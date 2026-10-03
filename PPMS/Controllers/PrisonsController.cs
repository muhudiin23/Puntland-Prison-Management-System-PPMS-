using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;
using PPMS.Models.ViewModels;

namespace PPMS.Controllers
{
    [Authorize]
    public class PrisonsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private const int PageSize = 12;

        public PrisonsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        // ── Index ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(string? search, string? security, int page = 1)
        {
            var userPrisonId = User.PrisonId();
            var superAdmin   = User.IsSuperAdmin();

            var query = _db.Prisons.AsQueryable();
            if (!superAdmin && userPrisonId.HasValue)
                query = query.Where(p => p.Id == userPrisonId.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.PrisonName.Contains(search) || p.City.Contains(search));
            if (!string.IsNullOrEmpty(security))
                query = query.Where(p => p.SecurityLevel == security);

            var paginated = await PaginatedList<Prison>.CreateAsync(query.OrderBy(p => p.PrisonName), page, PageSize);

            ViewBag.Search     = search;
            ViewBag.Security   = security;
            ViewBag.PageIndex  = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            return View(paginated);
        }

        // ── Create ────────────────────────────────────────────────────────────
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Create() => View(new CreatePrisonViewModel());

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Create(CreatePrisonViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (await _userManager.FindByNameAsync(model.AdminUsername) != null)
            {
                ModelState.AddModelError("AdminUsername", "Username is already taken.");
                return View(model);
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1 — Create the prison record
                var prison = new Prison
                {
                    PrisonName        = model.PrisonName,
                    City              = model.City,
                    Capacity          = model.Capacity,
                    CurrentPopulation = model.CurrentPopulation,
                    SecurityLevel     = model.SecurityLevel,
                    Address           = model.Address        ?? string.Empty,
                    ContactNumber     = model.ContactNumber  ?? string.Empty
                };
                _db.Prisons.Add(prison);
                await _db.SaveChangesAsync(); // obtains prison.Id within the transaction

                // 2 — Save profile photo if supplied
                string? photoPath = null;
                if (model.AdminProfilePhoto is { Length: > 0 })
                {
                    var ext = Path.GetExtension(model.AdminProfilePhoto.FileName).ToLowerInvariant();
                    if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
                    {
                        var dir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                        Directory.CreateDirectory(dir);
                        var fileName = $"{Guid.NewGuid()}{ext}";
                        using var fs = System.IO.File.Create(Path.Combine(dir, fileName));
                        await model.AdminProfilePhoto.CopyToAsync(fs);
                        photoPath = $"/uploads/profiles/{fileName}";
                    }
                }

                // 3 — Create the administrator user (UserManager uses the same DbContext/transaction)
                var admin = new ApplicationUser
                {
                    UserName        = model.AdminUsername,
                    Email           = model.AdminEmail,
                    FullName        = model.AdminFullName,
                    PhoneNumber     = model.AdminPhone,
                    Role            = "PrisonAdministrator",
                    AssignedPrisonId = prison.Id,
                    IsActive        = true,
                    EmailConfirmed  = true,
                    ProfilePhotoPath = photoPath
                };

                var result = await _userManager.CreateAsync(admin, model.AdminPassword);
                if (!result.Succeeded)
                {
                    await tx.RollbackAsync();
                    foreach (var e in result.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);
                    return View(model);
                }

                await _userManager.AddToRoleAsync(admin, "PrisonAdministrator");

                // 4 — Audit log
                _db.Activities.Add(new Activity
                {
                    Description  = $"Prison '{prison.PrisonName}' created with administrator '{admin.UserName}'.",
                    ActivityType = "Prison",
                    UserName     = User.Identity?.Name,
                    PrisonId     = prison.Id
                });
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = $"Prison '{prison.PrisonName}' and administrator account '{admin.UserName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return View(model);
            }
        }

        // ── Edit ──────────────────────────────────────────────────────────────
        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Edit(int id)
        {
            var prison = await _db.Prisons.FindAsync(id);
            if (prison == null) return NotFound();
            if (!CanManagePrison(id)) return Forbid();

            await LoadAdminViewBag(id);
            return View(prison);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Edit(int id, Prison model)
        {
            if (id != model.Id) return BadRequest();
            if (!CanManagePrison(id)) return Forbid();
            if (!ModelState.IsValid) { await LoadAdminViewBag(id); return View(model); }

            _db.Update(model);
            await _db.SaveChangesAsync();
            await LogActivity($"Prison '{model.PrisonName}' updated.", model.Id);
            TempData["Success"] = "Prison updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── ReassignAdmin ─────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ReassignAdmin(int prisonId, string? adminId)
        {
            var prison = await _db.Prisons.FindAsync(prisonId);
            if (prison == null) return NotFound();

            if (!string.IsNullOrEmpty(adminId))
            {
                var newAdmin = await _userManager.FindByIdAsync(adminId);
                if (newAdmin != null)
                {
                    newAdmin.AssignedPrisonId = prisonId;
                    await _userManager.UpdateAsync(newAdmin);
                    await LogActivity($"Administrator '{newAdmin.UserName}' assigned to '{prison.PrisonName}'.", prisonId);
                    TempData["Success"] = $"'{newAdmin.FullName}' is now the administrator of {prison.PrisonName}.";
                }
            }

            return RedirectToAction(nameof(Edit), new { id = prisonId });
        }

        // ── Details ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var prison = await _db.Prisons
                .Include(p => p.Prisoners)
                .Include(p => p.Staff)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (prison == null) return NotFound();
            if (!CanAccessPrison(id)) return Forbid();
            await LoadAdminViewBag(id);
            return View(prison);
        }

        // ── Delete ────────────────────────────────────────────────────────────
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var prison = await _db.Prisons.FindAsync(id);
            if (prison == null) return NotFound();
            return View(prison);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prison = await _db.Prisons
                .Include(p => p.Prisoners)
                .Include(p => p.Staff)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (prison == null) return NotFound();

            var activePrisoners = prison.Prisoners.Count(p => !p.IsArchived);
            if (activePrisoners > 0)
            {
                TempData["Error"] = $"Cannot delete '{prison.PrisonName}' — {activePrisoners} active prisoner(s) are still registered. Archive or transfer all prisoners first.";
                return RedirectToAction(nameof(Delete), new { id });
            }
            if (prison.Staff.Count > 0)
            {
                TempData["Error"] = $"Cannot delete '{prison.PrisonName}' — {prison.Staff.Count} staff member(s) still exist. Remove all staff records first.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            try
            {
                _db.Prisons.Remove(prison);
                await _db.SaveChangesAsync();
                await LogActivity($"Prison '{prison.PrisonName}' deleted.", null);
                TempData["Success"] = $"Prison '{prison.PrisonName}' has been permanently deleted.";
            }
            catch
            {
                TempData["Error"] = "An unexpected error occurred while deleting the prison. Please try again.";
                return RedirectToAction(nameof(Delete), new { id });
            }
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private async Task LoadAdminViewBag(int prisonId)
        {
            ViewBag.AssignedAdmin = await _db.Users
                .Where(u => u.AssignedPrisonId == prisonId && u.Role == "PrisonAdministrator")
                .FirstOrDefaultAsync();

            if (User.IsSuperAdmin())
            {
                var admins = await _db.Users
                    .Where(u => u.Role == "PrisonAdministrator")
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
                ViewBag.PrisonAdmins = new SelectList(admins, "Id", "FullName",
                    (ViewBag.AssignedAdmin as ApplicationUser)?.Id);
            }
        }

        private bool CanAccessPrison(int prisonId)
        {
            if (User.IsSuperAdmin()) return true;
            var uid = User.PrisonId();
            return uid.HasValue && uid.Value == prisonId;
        }

        private bool CanManagePrison(int prisonId)
        {
            if (User.IsSuperAdmin()) return true;
            var uid = User.PrisonId();
            return User.IsInRole("PrisonAdministrator") && uid.HasValue && uid.Value == prisonId;
        }

        private async Task LogActivity(string desc, int? prisonId)
        {
            _db.Activities.Add(new Activity
            {
                Description  = desc,
                ActivityType = "Prison",
                UserName     = User.Identity?.Name,
                PrisonId     = prisonId
            });
            await _db.SaveChangesAsync();
        }
    }
}
