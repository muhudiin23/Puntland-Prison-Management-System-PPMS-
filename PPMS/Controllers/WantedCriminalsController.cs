using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;

namespace PPMS.Controllers
{
    [Authorize]
    public class WantedCriminalsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const int PageSize = 12;

        public WantedCriminalsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index(string? search, string? riskLevel, int page = 1)
        {
            var query = _db.WantedCriminals.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(w => w.CriminalName.Contains(search) || w.NationalId.Contains(search) || w.CrimeDescription.Contains(search));
            if (!string.IsNullOrEmpty(riskLevel))
                query = query.Where(w => w.RiskLevel == riskLevel);

            var paginated = await PaginatedList<WantedCriminal>.CreateAsync(query.OrderByDescending(w => w.DateAdded), page, PageSize);

            ViewBag.Search = search;
            ViewBag.RiskLevel = riskLevel;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            return View(paginated);
        }

        public IActionResult Create() => View(new WantedCriminal { DateAdded = DateTime.Today });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WantedCriminal model, IFormFile? photo)
        {
            if (!ModelState.IsValid) return View(model);
            model.PhotoPath = await SavePhoto(photo);
            _db.WantedCriminals.Add(model);
            await _db.SaveChangesAsync();

            _db.Alerts.Add(new Alert
            {
                AlertType = "Wanted Criminal Alert",
                Message = $"New wanted criminal added: {model.CriminalName} — Risk Level: {model.RiskLevel}",
                Severity = model.RiskLevel,
                CreatedBy = User.Identity?.Name,
                IsActive = true
            });
            await _db.SaveChangesAsync();
            await LogActivity($"Wanted criminal '{model.CriminalName}' added (Risk: {model.RiskLevel}).");
            TempData["Success"] = "Wanted criminal record added and alert generated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.WantedCriminals.FindAsync(id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WantedCriminal model, IFormFile? photo)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);
            var existing = await _db.WantedCriminals.FindAsync(id);
            if (existing == null) return NotFound();
            existing.CriminalName = model.CriminalName;
            existing.NationalId = model.NationalId;
            existing.CrimeDescription = model.CrimeDescription;
            existing.LastKnownLocation = model.LastKnownLocation;
            existing.RiskLevel = model.RiskLevel;
            existing.IsActive = model.IsActive;
            if (photo != null) existing.PhotoPath = await SavePhoto(photo);
            await _db.SaveChangesAsync();
            await LogActivity($"Wanted criminal '{model.CriminalName}' updated.");
            TempData["Success"] = "Record updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var c = await _db.WantedCriminals.FindAsync(id);
            if (c == null) return NotFound();
            return View(c);
        }

        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.WantedCriminals.FindAsync(id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var c = await _db.WantedCriminals.FindAsync(id);
            if (c == null) return NotFound();
            if (!string.IsNullOrEmpty(c.PhotoPath))
            {
                var path = Path.Combine(_env.WebRootPath, c.PhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            _db.WantedCriminals.Remove(c);
            await _db.SaveChangesAsync();
            await LogActivity($"Wanted criminal '{c.CriminalName}' removed.");
            TempData["Success"] = "Record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string?> SavePhoto(IFormFile? photo)
        {
            if (photo == null || photo.Length == 0) return null;
            var ext = Path.GetExtension(photo.FileName).ToLower();
            if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(ext)) return null;
            var dir = Path.Combine(_env.WebRootPath, "uploads", "wanted");
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            await photo.CopyToAsync(stream);
            return $"/uploads/wanted/{fileName}";
        }

        private async Task LogActivity(string desc)
        {
            _db.Activities.Add(new Activity { Description = desc, ActivityType = "WantedCriminal", UserName = User.Identity?.Name });
            await _db.SaveChangesAsync();
        }
    }
}
