using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;

namespace PPMS.Controllers
{
    [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const int PageSize = 15;

        private static readonly string[] AllowedPhotoExt = [".jpg", ".jpeg", ".png", ".gif"];
        private static readonly string[] AllowedCertExt = [".pdf", ".docx", ".jpg", ".jpeg", ".png"];

        public StaffController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index(string? search, int? prisonId, string? role, int page = 1)
        {
            var query = _db.Staff.Include(s => s.Prison).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.FullName.Contains(search) || s.StaffIdNumber.Contains(search) || s.Email.Contains(search));
            if (prisonId.HasValue)
                query = query.Where(s => s.PrisonId == prisonId.Value);
            if (!string.IsNullOrEmpty(role))
                query = query.Where(s => s.Role == role);

            var paginated = await PaginatedList<Staff>.CreateAsync(query.OrderBy(s => s.FullName), page, PageSize);

            ViewBag.Search = search;
            ViewBag.PrisonId = prisonId;
            ViewBag.Role = role;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
            return View(paginated);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new Staff { IsActive = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Staff model, IFormFile? photo, List<IFormFile>? certificates)
        {
            ModelState.Remove("Prison");
            ModelState.Remove("Certificates");
            if (await _db.Staff.AnyAsync(s => s.StaffIdNumber == model.StaffIdNumber))
                ModelState.AddModelError("StaffIdNumber", "Staff ID already exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            if (photo != null && photo.Length > 0)
                model.PhotoPath = await SaveFile(photo, "staff/photos", AllowedPhotoExt, 5);

            _db.Staff.Add(model);
            await _db.SaveChangesAsync();

            if (certificates != null && certificates.Count > 0)
                await SaveCertificates(model.Id, certificates);

            await LogActivity($"Staff '{model.FullName}' (ID: {model.StaffIdNumber}) registered.");
            TempData["Success"] = $"Staff member '{model.FullName}' added successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var staff = await _db.Staff.FindAsync(id);
            if (staff == null) return NotFound();
            await PopulateDropdowns();
            return View(staff);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Staff model, IFormFile? photo, List<IFormFile>? certificates)
        {
            if (id != model.Id) return BadRequest();
            ModelState.Remove("Prison");
            ModelState.Remove("Certificates");
            if (await _db.Staff.AnyAsync(s => s.StaffIdNumber == model.StaffIdNumber && s.Id != id))
                ModelState.AddModelError("StaffIdNumber", "Another staff with this ID exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            if (photo != null && photo.Length > 0)
            {
                var existing = await _db.Staff.AsNoTracking().Select(s => new { s.Id, s.PhotoPath }).FirstOrDefaultAsync(s => s.Id == id);
                if (existing?.PhotoPath != null)
                    DeletePhysicalFile(existing.PhotoPath);
                model.PhotoPath = await SaveFile(photo, "staff/photos", AllowedPhotoExt, 5);
            }

            _db.Update(model);
            await _db.SaveChangesAsync();

            if (certificates != null && certificates.Count > 0)
                await SaveCertificates(model.Id, certificates);

            await LogActivity($"Staff '{model.FullName}' record updated.");
            TempData["Success"] = "Staff record updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var staff = await _db.Staff
                .Include(s => s.Prison)
                .Include(s => s.Certificates)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null) return NotFound();
            return View(staff);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var staff = await _db.Staff.Include(s => s.Prison).FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null) return NotFound();
            return View(staff);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var staff = await _db.Staff.Include(s => s.Certificates).FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null) return NotFound();

            if (staff.PhotoPath != null) DeletePhysicalFile(staff.PhotoPath);
            foreach (var cert in staff.Certificates)
                DeletePhysicalFile(cert.FilePath);

            _db.Staff.Remove(staff);
            await _db.SaveChangesAsync();
            await LogActivity($"Staff '{staff.FullName}' deleted.");
            TempData["Success"] = "Staff record deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── Certificate actions ──────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadCertificate(int staffId, List<IFormFile> certificates)
        {
            var staff = await _db.Staff.FindAsync(staffId);
            if (staff == null) return NotFound();
            if (certificates != null && certificates.Count > 0)
                await SaveCertificates(staffId, certificates);
            TempData["Success"] = "Certificate(s) uploaded.";
            return RedirectToAction(nameof(Details), new { id = staffId });
        }

        public async Task<IActionResult> DownloadCertificate(int id)
        {
            var cert = await _db.StaffCertificates.FindAsync(id);
            if (cert == null) return NotFound();
            var physPath = Path.Combine(_env.WebRootPath, cert.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(physPath)) return NotFound();
            var mime = GetMime(cert.FileType);
            return PhysicalFile(physPath, mime, cert.OriginalName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCertificate(int id)
        {
            var cert = await _db.StaffCertificates.FindAsync(id);
            if (cert == null) return NotFound();
            var staffId = cert.StaffId;
            DeletePhysicalFile(cert.FilePath);
            _db.StaffCertificates.Remove(cert);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Certificate deleted.";
            return RedirectToAction(nameof(Details), new { id = staffId });
        }

        // ── Helpers ──────────────────────────────────────────────────

        private async Task<string?> SaveFile(IFormFile file, string subfolder, string[] allowedExt, int maxMb)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext)) return null;
            if (file.Length > maxMb * 1024 * 1024) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", subfolder);
            Directory.CreateDirectory(folder);
            var filename = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, filename);
            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/{subfolder}/{filename}";
        }

        private async Task SaveCertificates(int staffId, List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                var path = await SaveFile(file, "staff/certificates", AllowedCertExt, 10);
                if (path == null) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant().TrimStart('.');
                _db.StaffCertificates.Add(new StaffCertificate
                {
                    StaffId = staffId,
                    FileName = Path.GetFileName(path),
                    FilePath = path,
                    OriginalName = file.FileName,
                    FileType = ext,
                    FileSize = file.Length,
                    UploadedAt = DateTime.Now,
                    UploadedBy = User.Identity?.Name
                });
            }
            await _db.SaveChangesAsync();
        }

        private void DeletePhysicalFile(string relativePath)
        {
            try
            {
                var physPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physPath))
                    System.IO.File.Delete(physPath);
            }
            catch { }
        }

        private static string GetMime(string ext) => ext.ToLower() switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            _ => "application/octet-stream"
        };

        private async Task PopulateDropdowns()
        {
            ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
        }

        private async Task LogActivity(string desc)
        {
            _db.Activities.Add(new Activity { Description = desc, ActivityType = "Staff", UserName = User.Identity?.Name });
            await _db.SaveChangesAsync();
        }
    }
}
