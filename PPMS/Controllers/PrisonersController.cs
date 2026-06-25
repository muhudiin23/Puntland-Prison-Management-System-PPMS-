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
    public class PrisonersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const int PageSize = 15;

        public PrisonersController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index(string? search, string? status, int? prisonId, string? sort, int page = 1)
        {
            // Only show active (non-archived) prisoners
            var query = _db.Prisoners.Include(p => p.Prison)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.FullName.Contains(search) || p.NationalId.Contains(search) || p.PrisonerId.Contains(search) || p.CrimeType.Contains(search));
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.CriminalStatus == status);
            if (prisonId.HasValue)
                query = query.Where(p => p.PrisonId == prisonId.Value);

            query = sort switch
            {
                "name"      => query.OrderBy(p => p.FullName),
                "name_desc" => query.OrderByDescending(p => p.FullName),
                "release"   => query.OrderBy(p => p.ReleaseDate),
                "crime"     => query.OrderBy(p => p.CrimeType),
                _           => query.OrderByDescending(p => p.CreatedAt)
            };

            var paginated = await PaginatedList<Prisoner>.CreateAsync(query, page, PageSize);

            ViewBag.Search    = search;
            ViewBag.Status    = status;
            ViewBag.PrisonId  = prisonId;
            ViewBag.Sort      = sort;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            ViewBag.Prisons   = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
            return View(paginated);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new Prisoner { EntryDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Prisoner model, IFormFile? photo)
        {
            ModelState.Remove("Prison");
            if (await _db.Prisoners.AnyAsync(p => p.PrisonerId == model.PrisonerId))
                ModelState.AddModelError("PrisonerId", "A prisoner with this ID already exists.");
            if (await _db.Prisoners.AnyAsync(p => p.NationalId == model.NationalId && !p.IsArchived))
                ModelState.AddModelError("NationalId", "An active prisoner with this National ID already exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            model.ReleaseDate = model.EntryDate.AddMonths(model.SentenceDurationMonths);
            model.PhotoPath   = await SavePhoto(photo, "photos");

            _db.Prisoners.Add(model);
            await _db.SaveChangesAsync();
            await UpdatePrisonPopulation(model.PrisonId);
            await LogActivity($"Prisoner '{model.FullName}' (ID: {model.PrisonerId}) registered.", "Prisoner");
            TempData["Success"] = $"Prisoner '{model.FullName}' registered successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var prisoner = await _db.Prisoners.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (prisoner == null) return NotFound();
            await PopulateDropdowns();
            return View(prisoner);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Prisoner model, IFormFile? photo)
        {
            if (id != model.Id) return BadRequest();
            ModelState.Remove("Prison");
            if (await _db.Prisoners.AnyAsync(p => p.PrisonerId == model.PrisonerId && p.Id != id))
                ModelState.AddModelError("PrisonerId", "Another prisoner with this ID already exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            var existing = await _db.Prisoners.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (existing == null) return NotFound();

            existing.FullName             = model.FullName;
            existing.NationalId           = model.NationalId;
            existing.PrisonerId           = model.PrisonerId;
            existing.Gender               = model.Gender;
            existing.DateOfBirth          = model.DateOfBirth;
            existing.CrimeType            = model.CrimeType;
            existing.SentenceDurationMonths = model.SentenceDurationMonths;
            existing.EntryDate            = model.EntryDate;
            existing.ReleaseDate          = model.EntryDate.AddMonths(model.SentenceDurationMonths);
            existing.CriminalStatus       = model.CriminalStatus;
            existing.Address              = model.Address;
            existing.EmergencyContact     = model.EmergencyContact;
            existing.PrisonId             = model.PrisonId;
            existing.FingerprintData      = model.FingerprintData;
            existing.UpdatedAt            = DateTime.Now;

            if (photo != null) existing.PhotoPath = await SavePhoto(photo, "photos");

            await _db.SaveChangesAsync();
            await UpdatePrisonPopulation(model.PrisonId);
            await LogActivity($"Prisoner '{model.FullName}' record updated.", "Prisoner");
            TempData["Success"] = "Prisoner record updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var prisoner = await _db.Prisoners.Include(p => p.Prison).FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (prisoner == null) return NotFound();
            return View(prisoner);
        }

        // GET — shows Archive confirmation form (replaces hard-delete)
        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var prisoner = await _db.Prisoners.Include(p => p.Prison).FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (prisoner == null) return NotFound();
            return View(prisoner);
        }

        // POST — archives the prisoner (never hard-deletes)
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id, string? archiveReason, string? archiveNotes)
        {
            var prisoner = await _db.Prisoners.FindAsync(id);
            if (prisoner == null) return NotFound();

            prisoner.IsArchived    = true;
            prisoner.ArchivedAt    = DateTime.Now;
            prisoner.ArchiveReason = string.IsNullOrEmpty(archiveReason) ? "Administrative" : archiveReason;
            prisoner.ArchivedBy    = User.Identity?.Name;
            prisoner.ArchiveNotes  = archiveNotes;
            prisoner.UpdatedAt     = DateTime.Now;

            // Update criminal status to match archive reason if it's a known final status
            if (prisoner.ArchiveReason is "Released" or "Transferred" or "Deceased")
                prisoner.CriminalStatus = prisoner.ArchiveReason;

            await _db.SaveChangesAsync();
            await UpdatePrisonPopulation(prisoner.PrisonId);
            await LogActivity($"Prisoner '{prisoner.FullName}' (ID: {prisoner.PrisonerId}) archived. Reason: {prisoner.ArchiveReason}.", "Archive");
            TempData["Success"] = $"'{prisoner.FullName}' has been moved to the Former Prisoners Archive.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string?> SavePhoto(IFormFile? photo, string folder)
        {
            if (photo == null || photo.Length == 0) return null;
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var ext = Path.GetExtension(photo.FileName).ToLower();
            if (!allowedExt.Contains(ext)) return null;
            var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            await photo.CopyToAsync(stream);
            return $"/uploads/{folder}/{fileName}";
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

        private async Task PopulateDropdowns()
        {
            ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
        }

        private async Task LogActivity(string desc, string type)
        {
            _db.Activities.Add(new Activity { Description = desc, ActivityType = type, UserName = User.Identity?.Name });
            await _db.SaveChangesAsync();
        }
    }
}
