using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;

namespace PPMS.Controllers
{
    [Authorize]
    public class TransfersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private const int PageSize = 20;

        public TransfersController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? status, int page = 1)
        {
            var userPrisonId = User.PrisonId();
            var superAdmin = User.IsSuperAdmin();

            var query = _db.PrisonerTransfers
                .Include(t => t.Prisoner)
                .Include(t => t.SourcePrison)
                .Include(t => t.DestinationPrison)
                .AsQueryable();

            // SuperAdmin sees all; others see transfers involving their prison
            if (!superAdmin && userPrisonId.HasValue)
                query = query.Where(t => t.SourcePrisonId == userPrisonId || t.DestinationPrisonId == userPrisonId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            var paginated = await PaginatedList<PrisonerTransfer>.CreateAsync(
                query.OrderByDescending(t => t.RequestedAt), page, PageSize);

            ViewBag.Status = status;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            ViewBag.PendingCount = await _db.PrisonerTransfers.CountAsync(t => t.Status == "Pending");
            return View(paginated);
        }

        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new PrisonerTransfer { RequestedAt = DateTime.Now });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Create(PrisonerTransfer model)
        {
            ModelState.Remove("Prisoner");
            ModelState.Remove("SourcePrison");
            ModelState.Remove("DestinationPrison");
            ModelState.Remove("RequestedBy");
            ModelState.Remove("Status");

            if (model.SourcePrisonId == model.DestinationPrisonId)
                ModelState.AddModelError("DestinationPrisonId", "Source and destination prisons must be different.");

            // Ensure non-SuperAdmin can only request from their own prison
            if (!User.IsSuperAdmin())
            {
                var userPrisonId = User.PrisonId();
                if (userPrisonId.HasValue) model.SourcePrisonId = userPrisonId.Value;
            }

            var prisoner = await _db.Prisoners.FindAsync(model.PrisonerId);
            if (prisoner == null)
                ModelState.AddModelError("PrisonerId", "Prisoner not found.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            model.Status = "Pending";
            model.RequestedBy = User.Identity?.Name ?? "";
            model.RequestedAt = DateTime.Now;
            model.CreatedAt = DateTime.Now;

            _db.PrisonerTransfers.Add(model);
            await LogActivity($"Transfer requested for prisoner '{prisoner!.FullName}' from prison {model.SourcePrisonId} to {model.DestinationPrisonId}.", model.SourcePrisonId);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Transfer request submitted successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var transfer = await _db.PrisonerTransfers
                .Include(t => t.Prisoner)
                .Include(t => t.SourcePrison)
                .Include(t => t.DestinationPrison)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null) return NotFound();
            if (!CanViewTransfer(transfer)) return Forbid();
            return View(transfer);
        }

        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Review(int id)
        {
            var transfer = await _db.PrisonerTransfers
                .Include(t => t.Prisoner)
                .Include(t => t.SourcePrison)
                .Include(t => t.DestinationPrison)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null) return NotFound();
            if (!CanReviewTransfer(transfer)) return Forbid();
            if (transfer.Status != "Pending") return RedirectToAction(nameof(Details), new { id });
            return View(transfer);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Approve(int id, string? notes)
        {
            var transfer = await _db.PrisonerTransfers
                .Include(t => t.Prisoner)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null) return NotFound();
            if (!CanReviewTransfer(transfer)) return Forbid();
            if (transfer.Status != "Pending") { TempData["Error"] = "Transfer is not in Pending status."; return RedirectToAction(nameof(Index)); }

            transfer.Status = "Approved";
            transfer.ReviewedBy = User.Identity?.Name;
            transfer.ReviewedAt = DateTime.Now;
            transfer.TransferNotes = notes ?? transfer.TransferNotes;

            await LogActivity($"Transfer #{id} for '{transfer.Prisoner?.FullName}' approved.", transfer.SourcePrisonId);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Transfer approved. It can now be completed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Reject(int id, string? rejectionReason)
        {
            var transfer = await _db.PrisonerTransfers
                .Include(t => t.Prisoner)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null) return NotFound();
            if (!CanReviewTransfer(transfer)) return Forbid();
            if (transfer.Status != "Pending") { TempData["Error"] = "Transfer is not in Pending status."; return RedirectToAction(nameof(Index)); }

            transfer.Status = "Rejected";
            transfer.ReviewedBy = User.Identity?.Name;
            transfer.ReviewedAt = DateTime.Now;
            transfer.RejectionReason = rejectionReason;

            await LogActivity($"Transfer #{id} for '{transfer.Prisoner?.FullName}' rejected.", transfer.SourcePrisonId);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Transfer rejected.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Complete(int id)
        {
            var transfer = await _db.PrisonerTransfers
                .Include(t => t.Prisoner)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null) return NotFound();
            if (!CanReviewTransfer(transfer)) return Forbid();
            if (transfer.Status != "Approved") { TempData["Error"] = "Transfer must be approved before completing."; return RedirectToAction(nameof(Index)); }

            // Move prisoner to the destination prison
            var prisoner = transfer.Prisoner ?? await _db.Prisoners.FindAsync(transfer.PrisonerId);
            if (prisoner == null) { TempData["Error"] = "Prisoner record not found."; return RedirectToAction(nameof(Index)); }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                prisoner.PrisonId = transfer.DestinationPrisonId;
                prisoner.UpdatedAt = DateTime.Now;

                transfer.Status = "Completed";
                transfer.CompletedAt = DateTime.Now;
                transfer.AuthorizedBy = User.Identity?.Name;

                await _db.SaveChangesAsync();

                // Update population counts for both prisons
                await UpdatePrisonPopulation(transfer.SourcePrisonId);
                await UpdatePrisonPopulation(transfer.DestinationPrisonId);

                await LogActivity($"Transfer #{id} for '{prisoner.FullName}' completed. Moved to prison {transfer.DestinationPrisonId}.", transfer.DestinationPrisonId);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "An error occurred while completing the transfer.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Transfer completed. {prisoner.FullName} has been moved to the destination prison.";
            return RedirectToAction(nameof(Index));
        }

        private bool CanViewTransfer(PrisonerTransfer t)
        {
            if (User.IsSuperAdmin()) return true;
            var userPrisonId = User.PrisonId();
            return userPrisonId.HasValue && (t.SourcePrisonId == userPrisonId || t.DestinationPrisonId == userPrisonId);
        }

        private bool CanReviewTransfer(PrisonerTransfer t)
        {
            if (User.IsSuperAdmin()) return true;
            if (!User.IsInRole("PrisonAdministrator")) return false;
            var userPrisonId = User.PrisonId();
            return userPrisonId.HasValue && (t.SourcePrisonId == userPrisonId || t.DestinationPrisonId == userPrisonId);
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
        }

        private async Task UpdatePrisonPopulation(int prisonId)
        {
            var prison = await _db.Prisons.FindAsync(prisonId);
            if (prison != null)
            {
                prison.CurrentPopulation = await _db.Prisoners.CountAsync(p =>
                    p.PrisonId == prisonId && p.CriminalStatus == "Active" && !p.IsArchived);
                await _db.SaveChangesAsync();
            }
        }

        private async Task LogActivity(string desc, int? prisonId = null)
        {
            _db.Activities.Add(new Activity
            {
                Description = desc,
                ActivityType = "Transfer",
                UserName = User.Identity?.Name,
                PrisonId = prisonId ?? User.PrisonId()
            });
            await _db.SaveChangesAsync();
        }
    }
}
