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
            var userPrisonId = User.PrisonId();
            var superAdmin = User.IsSuperAdmin();

            var query = _db.Prisoners.Include(p => p.Prison)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            // Server-side isolation: non-SuperAdmin always scoped to their prison
            if (!superAdmin && userPrisonId.HasValue)
                query = query.Where(p => p.PrisonId == userPrisonId.Value);
            else if (superAdmin && prisonId.HasValue)
                query = query.Where(p => p.PrisonId == prisonId.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.FullName.Contains(search) || p.NationalId.Contains(search) || p.PrisonerId.Contains(search) || p.CrimeType.Contains(search));
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.CriminalStatus == status);

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
            if (superAdmin)
                ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
            return View(paginated);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new Prisoner { EntryDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Prisoner model, IFormFile? photo, List<IFormFile>? evidenceFiles, string? documentType, string? description)
        {
            ModelState.Remove("Prison");
            ModelState.Remove("Evidences");
            if (await _db.Prisoners.AnyAsync(p => p.PrisonerId == model.PrisonerId))
                ModelState.AddModelError("PrisonerId", "A prisoner with this ID already exists.");
            if (await _db.Prisoners.AnyAsync(p => p.NationalId == model.NationalId && !p.IsArchived))
                ModelState.AddModelError("NationalId", "An active prisoner with this National ID already exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            model.ReleaseDate = model.EntryDate.AddMonths(model.SentenceDurationMonths);
            model.PhotoPath   = await SavePhoto(photo, "photos");

            _db.Prisoners.Add(model);
            await _db.SaveChangesAsync();

            if (evidenceFiles != null && evidenceFiles.Count > 0)
                await SaveEvidenceFiles(model.Id, evidenceFiles, documentType ?? "Other", description);

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
        public async Task<IActionResult> Edit(int id, Prisoner model, IFormFile? photo, List<IFormFile>? evidenceFiles, string? documentType, string? description)
        {
            if (id != model.Id) return BadRequest();
            ModelState.Remove("Prison");
            ModelState.Remove("Evidences");
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

            if (evidenceFiles != null && evidenceFiles.Count > 0)
                await SaveEvidenceFiles(id, evidenceFiles, documentType ?? "Other", description);

            await UpdatePrisonPopulation(model.PrisonId);
            await LogActivity($"Prisoner '{model.FullName}' record updated.", "Prisoner");
            TempData["Success"] = "Prisoner record updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var prisoner = await _db.Prisoners
                .Include(p => p.Prison)
                .Include(p => p.Evidences)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (prisoner == null) return NotFound();
            if (!CanAccessPrison(prisoner.PrisonId)) return Forbid();
            return View(prisoner);
        }

        // ── Evidence actions (SuperAdmin + PrisonAdministrator only) ──

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> UploadEvidence(int prisonerId, List<IFormFile> evidenceFiles, string? documentType, string? description)
        {
            var prisoner = await _db.Prisoners.FindAsync(prisonerId);
            if (prisoner == null) return NotFound();
            if (evidenceFiles != null && evidenceFiles.Count > 0)
                await SaveEvidenceFiles(prisonerId, evidenceFiles, documentType ?? "Other", description);
            TempData["Success"] = "Evidence document(s) uploaded.";
            return RedirectToAction(nameof(Details), new { id = prisonerId });
        }

        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DownloadEvidence(int id)
        {
            var ev = await _db.PrisonerEvidences.FindAsync(id);
            if (ev == null) return NotFound();
            var physPath = Path.Combine(_env.WebRootPath, ev.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(physPath)) return NotFound();
            var mime = GetMime(ev.FileType);
            return PhysicalFile(physPath, mime, ev.OriginalName);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DeleteEvidence(int id)
        {
            var ev = await _db.PrisonerEvidences.FindAsync(id);
            if (ev == null) return NotFound();
            var prisonerId = ev.PrisonerId;
            DeletePhysicalFile(ev.FilePath);
            _db.PrisonerEvidences.Remove(ev);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Evidence document deleted.";
            return RedirectToAction(nameof(Details), new { id = prisonerId });
        }

        // GET — shows Archive confirmation form (replaces hard-delete)
        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var prisoner = await _db.Prisoners.Include(p => p.Prison).FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (prisoner == null) return NotFound();
            if (!CanAccessPrison(prisoner.PrisonId)) return Forbid();
            return View(prisoner);
        }

        // POST — copies prisoner to FormerPrisoners table, then hard-deletes from Prisoners
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id, string? archiveReason, string? archiveNotes)
        {
            var prisoner = await _db.Prisoners
                .Include(p => p.Prison)
                .Include(p => p.Evidences)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (prisoner == null) return NotFound();

            var finalReason = string.IsNullOrEmpty(archiveReason) ? "Administrative" : archiveReason;
            var finalStatus = finalReason is "Released" or "Transferred" or "Deceased"
                ? finalReason : prisoner.CriminalStatus;

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.FormerPrisoners.Add(new FormerPrisoner
                {
                    PrisonerId             = prisoner.PrisonerId,
                    FullName               = prisoner.FullName,
                    NationalId             = prisoner.NationalId,
                    Gender                 = prisoner.Gender,
                    DateOfBirth            = prisoner.DateOfBirth,
                    CrimeType              = prisoner.CrimeType,
                    SentenceDurationMonths = prisoner.SentenceDurationMonths,
                    EntryDate              = prisoner.EntryDate,
                    ReleaseDate            = prisoner.ReleaseDate,
                    CriminalStatus         = finalStatus,
                    Address                = prisoner.Address,
                    EmergencyContact       = prisoner.EmergencyContact,
                    PhotoPath              = prisoner.PhotoPath,
                    FingerprintData        = prisoner.FingerprintData,
                    OriginalPrisonId       = prisoner.PrisonId,
                    PrisonName             = prisoner.Prison?.PrisonName ?? string.Empty,
                    PrisonCity             = prisoner.Prison?.City,
                    OriginalCreatedAt      = prisoner.CreatedAt,
                    OriginalUpdatedAt      = prisoner.UpdatedAt,
                    ArchivedAt             = DateTime.Now,
                    ArchiveReason          = finalReason,
                    ArchivedBy             = User.Identity?.Name,
                    ArchiveNotes           = archiveNotes,
                    EvidenceCount          = prisoner.Evidences.Count,
                    RecordMovedAt          = DateTime.Now
                });
                await _db.SaveChangesAsync();

                // Remove any transfer records before deleting (Restrict FK would block)
                var transfers = await _db.PrisonerTransfers
                    .Where(t => t.PrisonerId == prisoner.Id)
                    .ToListAsync();
                if (transfers.Count > 0)
                    _db.PrisonerTransfers.RemoveRange(transfers);

                // Hard-delete prisoner; cascade deletes PrisonerEvidence rows
                _db.Prisoners.Remove(prisoner);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "An error occurred while archiving the prisoner. No data was lost. Please try again.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            await UpdatePrisonPopulation(prisoner.PrisonId);
            await LogActivity($"Prisoner '{prisoner.FullName}' (ID: {prisoner.PrisonerId}) archived. Reason: {finalReason}.", "Archive");
            TempData["Success"] = $"'{prisoner.FullName}' has been moved to the Former Prisoners Archive.";
            return RedirectToAction(nameof(Index));
        }

        private static readonly string[] AllowedEvidenceExt = [".pdf", ".docx", ".jpg", ".jpeg", ".png"];

        private async Task SaveEvidenceFiles(int prisonerId, List<IFormFile> files, string docType, string? description)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "prisoners", "evidence");
            Directory.CreateDirectory(dir);
            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedEvidenceExt.Contains(ext)) continue;
                if (file.Length > 20 * 1024 * 1024) continue;
                var filename = Guid.NewGuid().ToString("N") + ext;
                var fullPath = Path.Combine(dir, filename);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);
                _db.PrisonerEvidences.Add(new PrisonerEvidence
                {
                    PrisonerId = prisonerId,
                    FileName = filename,
                    FilePath = $"/uploads/prisoners/evidence/{filename}",
                    OriginalName = file.FileName,
                    FileType = ext.TrimStart('.'),
                    FileSize = file.Length,
                    DocumentType = docType,
                    Description = description,
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

        private bool CanAccessPrison(int prisonId)
        {
            if (User.IsSuperAdmin()) return true;
            var userPrisonId = User.PrisonId();
            return userPrisonId.HasValue && userPrisonId.Value == prisonId;
        }

        private async Task PopulateDropdowns()
        {
            if (User.IsSuperAdmin())
            {
                ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
            }
            else
            {
                var prisonId = User.PrisonId();
                var prisons = prisonId.HasValue
                    ? await _db.Prisons.Where(p => p.Id == prisonId.Value).ToListAsync()
                    : new List<Prison>();
                ViewBag.Prisons = new SelectList(prisons, "Id", "PrisonName");
            }

            ViewBag.CrimeTypes = new SelectList(new[]
            {
                "Murder / Homicide",
                "Armed Robbery",
                "Theft / Burglary",
                "Assault / Battery",
                "Kidnapping / Abduction",
                "Sexual Assault / Rape",
                "Drug Trafficking",
                "Illegal Weapons Possession",
                "Terrorism / Extremism",
                "Piracy",
                "Human Trafficking",
                "Fraud / Forgery",
                "Corruption / Bribery",
                "Arson",
                "Vandalism / Property Damage",
                "Other"
            });
        }

        private async Task LogActivity(string desc, string type, int? prisonId = null)
        {
            _db.Activities.Add(new Activity { Description = desc, ActivityType = type, UserName = User.Identity?.Name, PrisonId = prisonId ?? User.PrisonId() });
            await _db.SaveChangesAsync();
        }
    }
}
