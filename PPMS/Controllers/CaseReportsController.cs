using ClosedXML.Excel;
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
    public class CaseReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const int PageSize = 20;

        public static readonly string[] CrimeCategories =
        {
            "Theft", "Robbery", "Fraud", "Murder", "Assault",
            "Drug Trafficking", "Human Trafficking", "Terrorism", "Cybercrime", "Other"
        };

        public static readonly string[] InvestigationStatuses =
            { "Pending", "Ongoing", "Completed", "Suspended" };

        public static readonly string[] CaseStatuses =
            { "Open", "Under Review", "Closed", "Dismissed" };

        public CaseReportsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // ── Index ─────────────────────────────────────────────────
        public async Task<IActionResult> Index(
            string? search, string? crimeType, int? prisonId,
            string? caseStatus, string? from, string? to,
            string? sort, int page = 1)
        {
            var query = _db.CaseReports.Include(c => c.Prison).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c =>
                    c.CaseId.Contains(search)         ||
                    c.CaseTitle.Contains(search)      ||
                    c.PrisonerName.Contains(search)   ||
                    c.NationalId.Contains(search)     ||
                    c.OfficerInCharge.Contains(search));

            if (!string.IsNullOrEmpty(crimeType))
                query = query.Where(c => c.CrimeType == crimeType);

            if (prisonId.HasValue)
                query = query.Where(c => c.PrisonId == prisonId.Value);

            if (!string.IsNullOrEmpty(caseStatus))
                query = query.Where(c => c.CaseStatus == caseStatus);

            if (DateTime.TryParse(from, out var fromDt))
                query = query.Where(c => c.DateOfCrime >= fromDt);

            if (DateTime.TryParse(to, out var toDt))
                query = query.Where(c => c.DateOfCrime <= toDt.AddDays(1));

            query = sort switch
            {
                "title"     => query.OrderBy(c => c.CaseTitle),
                "crime"     => query.OrderBy(c => c.CrimeType),
                "date_asc"  => query.OrderBy(c => c.DateOfCrime),
                "reported"  => query.OrderByDescending(c => c.DateReported),
                _           => query.OrderByDescending(c => c.DateOfCrime)
            };

            var paginated = await PaginatedList<CaseReport>.CreateAsync(query, page, PageSize);

            ViewBag.Search       = search;
            ViewBag.CrimeType    = crimeType;
            ViewBag.PrisonId     = prisonId;
            ViewBag.CaseStatus   = caseStatus;
            ViewBag.From         = from;
            ViewBag.To           = to;
            ViewBag.Sort         = sort;
            ViewBag.PageIndex    = paginated.PageIndex;
            ViewBag.TotalPages   = paginated.TotalPages;
            ViewBag.TotalCount   = paginated.TotalCount;
            ViewBag.Categories   = CrimeCategories;
            ViewBag.CaseStatuses = CaseStatuses;
            ViewBag.Prisons      = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");

            return View(paginated);
        }

        // ── Details ───────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var c = await _db.CaseReports.Include(x => x.Prison).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            return View(c);
        }

        // ── Create ────────────────────────────────────────────────
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new CaseReport
            {
                CaseId       = await GenerateCaseId(),
                DateOfCrime  = DateTime.Today,
                DateReported = DateTime.Today
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CaseReport model, IFormFile? document, IFormFile? evidence)
        {
            ModelState.Remove("Prison");
            if (await _db.CaseReports.AnyAsync(c => c.CaseId == model.CaseId))
                model.CaseId = await GenerateCaseId();

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            model.DocumentPath = await SaveFile(document, "cases/documents");
            model.EvidencePath = await SaveFile(evidence, "cases/evidence");
            model.CreatedBy    = User.Identity?.Name;

            _db.CaseReports.Add(model);
            await _db.SaveChangesAsync();

            await LogActivity($"Case '{model.CaseId}' ({model.CrimeType}) registered.", "CaseReport");
            TempData["Success"] = $"Case {model.CaseId} registered successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── Edit ──────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.CaseReports.FindAsync(id);
            if (c == null) return NotFound();
            await PopulateDropdowns();
            return View(c);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CaseReport model, IFormFile? document, IFormFile? evidence)
        {
            if (id != model.Id) return BadRequest();
            ModelState.Remove("Prison");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            var existing = await _db.CaseReports.FindAsync(id);
            if (existing == null) return NotFound();

            existing.CaseTitle           = model.CaseTitle;
            existing.CrimeType           = model.CrimeType;
            existing.CrimeDescription    = model.CrimeDescription;
            existing.PrisonerName        = model.PrisonerName;
            existing.NationalId          = model.NationalId;
            existing.PrisonId            = model.PrisonId;
            existing.DateOfCrime         = model.DateOfCrime;
            existing.DateReported        = model.DateReported;
            existing.InvestigationStatus = model.InvestigationStatus;
            existing.CaseStatus          = model.CaseStatus;
            existing.OfficerInCharge     = model.OfficerInCharge;
            existing.Notes               = model.Notes;
            existing.UpdatedAt           = DateTime.Now;

            if (document != null) existing.DocumentPath = await SaveFile(document, "cases/documents");
            if (evidence != null) existing.EvidencePath = await SaveFile(evidence, "cases/evidence");

            await _db.SaveChangesAsync();
            await LogActivity($"Case '{existing.CaseId}' updated.", "CaseReport");
            TempData["Success"] = $"Case {existing.CaseId} updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── Delete ────────────────────────────────────────────────
        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.CaseReports.Include(x => x.Prison).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var c = await _db.CaseReports.FindAsync(id);
            if (c == null) return NotFound();
            DeleteFile(c.DocumentPath);
            DeleteFile(c.EvidencePath);
            _db.CaseReports.Remove(c);
            await _db.SaveChangesAsync();
            await LogActivity($"Case '{c.CaseId}' deleted.", "CaseReport");
            TempData["Success"] = $"Case {c.CaseId} deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── Analytics Dashboard ───────────────────────────────────
        public async Task<IActionResult> Analytics(string? from, string? to)
        {
            var all = _db.CaseReports.Include(c => c.Prison).AsQueryable();

            // Summary stats
            ViewBag.TotalCases   = await all.CountAsync();
            ViewBag.OpenCases    = await all.CountAsync(c => c.CaseStatus == "Open");
            ViewBag.ClosedCases  = await all.CountAsync(c => c.CaseStatus == "Closed");
            ViewBag.UnderReview  = await all.CountAsync(c => c.CaseStatus == "Under Review");
            ViewBag.Dismissed    = await all.CountAsync(c => c.CaseStatus == "Dismissed");

            // Most common crime type overall
            var topCrime = await all
                .GroupBy(c => c.CrimeType)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .FirstOrDefaultAsync();
            ViewBag.MostCommonCrime      = topCrime?.type ?? "N/A";
            ViewBag.MostCommonCrimeCount = topCrime?.count ?? 0;

            // Prison with most cases
            var topPrison = await _db.CaseReports
                .Where(c => c.PrisonId != null)
                .Include(c => c.Prison)
                .GroupBy(c => c.Prison!.PrisonName)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .FirstOrDefaultAsync();
            ViewBag.HighestCasePrison      = topPrison?.name ?? "N/A";
            ViewBag.HighestCasePrisonCount = topPrison?.count ?? 0;

            // Crime trend prediction: compare last 6 months vs previous 6 months
            var now      = DateTime.Now;
            var half1End = now;
            var half1Start = now.AddMonths(-6);
            var half2End = half1Start.AddDays(-1);
            var half2Start = now.AddMonths(-12);

            var recentByCrime   = await all.Where(c => c.DateOfCrime >= half1Start).GroupBy(c => c.CrimeType).Select(g => new { type = g.Key, count = g.Count() }).ToListAsync();
            var previousByCrime = await all.Where(c => c.DateOfCrime >= half2Start && c.DateOfCrime < half1Start).GroupBy(c => c.CrimeType).Select(g => new { type = g.Key, count = g.Count() }).ToListAsync();

            var trendData = CrimeCategories.Select(cat =>
            {
                int recent   = recentByCrime.FirstOrDefault(x => x.type == cat)?.count   ?? 0;
                int previous = previousByCrime.FirstOrDefault(x => x.type == cat)?.count ?? 0;
                double pct   = previous > 0 ? Math.Round((double)(recent - previous) / previous * 100, 1) : (recent > 0 ? 100 : 0);
                return new { crime = cat, recent, previous, pct, trend = pct > 5 ? "up" : pct < -5 ? "down" : "stable" };
            }).OrderByDescending(x => x.pct).ToList();

            ViewBag.TrendData = trendData;

            // Date range analysis
            if (DateTime.TryParse(from, out var fromDt) && DateTime.TryParse(to, out var toDt))
            {
                var range = all.Where(c => c.DateOfCrime >= fromDt && c.DateOfCrime <= toDt.AddDays(1));

                var rangeCount  = await range.CountAsync();
                var rangeCrimes = await range.GroupBy(c => c.CrimeType)
                    .Select(g => new { type = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count).ToListAsync();

                // Previous period of same duration
                int days      = (int)(toDt - fromDt).TotalDays + 1;
                var prevFrom  = fromDt.AddDays(-days);
                var prevTo    = fromDt.AddDays(-1);
                var prevCount = await all.CountAsync(c => c.DateOfCrime >= prevFrom && c.DateOfCrime <= prevTo);
                double growth = prevCount > 0 ? Math.Round((double)(rangeCount - prevCount) / prevCount * 100, 1) : (rangeCount > 0 ? 100.0 : 0.0);

                int total = rangeCount > 0 ? rangeCount : 1;
                var top   = rangeCrimes.FirstOrDefault();
                var bot   = rangeCrimes.LastOrDefault();

                ViewBag.RangeFrom    = fromDt;
                ViewBag.RangeTo      = toDt;
                ViewBag.RangeCount   = rangeCount;
                ViewBag.RangeCrimes  = rangeCrimes.Select(x => new { x.type, x.count, pct = Math.Round((double)x.count / total * 100, 1) }).ToList();
                ViewBag.GrowthPct    = growth;
                ViewBag.PrevCount    = prevCount;
                ViewBag.TopCrime     = top?.type;
                ViewBag.TopCrimePct  = top != null ? Math.Round((double)top.count / total * 100, 1) : 0.0;
                ViewBag.LowCrime     = bot?.type;
                ViewBag.LowCrimePct  = bot != null ? Math.Round((double)bot.count / total * 100, 1) : 0.0;
            }

            ViewBag.From = from;
            ViewBag.To   = to;

            return View();
        }

        // ── Excel Export ──────────────────────────────────────────
        public async Task<IActionResult> ExportExcel(string? crimeType, string? caseStatus, int? prisonId, string? from, string? to)
        {
            var query = _db.CaseReports.Include(c => c.Prison).AsQueryable();

            if (!string.IsNullOrEmpty(crimeType))    query = query.Where(c => c.CrimeType  == crimeType);
            if (!string.IsNullOrEmpty(caseStatus))   query = query.Where(c => c.CaseStatus == caseStatus);
            if (prisonId.HasValue)                   query = query.Where(c => c.PrisonId   == prisonId.Value);
            if (DateTime.TryParse(from, out var fd)) query = query.Where(c => c.DateOfCrime >= fd);
            if (DateTime.TryParse(to,   out var td)) query = query.Where(c => c.DateOfCrime <= td.AddDays(1));

            var data = await query.OrderByDescending(c => c.DateOfCrime).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Case Reports");

            var headers = new[]
            {
                "Case ID","Case Title","Crime Type","Prisoner Name","National ID","Prison",
                "Date of Crime","Date Reported","Investigation Status","Case Status",
                "Officer in Charge","Notes","Created By","Created At"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d1b2a");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int i = 0; i < data.Count; i++)
            {
                var c = data[i]; int r = i + 2;
                ws.Cell(r, 1).Value  = c.CaseId;
                ws.Cell(r, 2).Value  = c.CaseTitle;
                ws.Cell(r, 3).Value  = c.CrimeType;
                ws.Cell(r, 4).Value  = c.PrisonerName;
                ws.Cell(r, 5).Value  = c.NationalId;
                ws.Cell(r, 6).Value  = c.Prison?.PrisonName ?? "";
                ws.Cell(r, 7).Value  = c.DateOfCrime.ToString("yyyy-MM-dd");
                ws.Cell(r, 8).Value  = c.DateReported.ToString("yyyy-MM-dd");
                ws.Cell(r, 9).Value  = c.InvestigationStatus;
                ws.Cell(r, 10).Value = c.CaseStatus;
                ws.Cell(r, 11).Value = c.OfficerInCharge;
                ws.Cell(r, 12).Value = c.Notes ?? "";
                ws.Cell(r, 13).Value = c.CreatedBy ?? "";
                ws.Cell(r, 14).Value = c.CreatedAt.ToString("yyyy-MM-dd HH:mm");

                var rowColor = c.CaseStatus switch
                {
                    "Open"         => XLColor.FromHtml("#fff7ed"),
                    "Under Review" => XLColor.FromHtml("#fef9c3"),
                    "Closed"       => XLColor.FromHtml("#f0fdf4"),
                    _              => XLColor.FromHtml("#f9fafb")
                };
                for (int col = 1; col <= headers.Length; col++)
                    ws.Cell(r, col).Style.Fill.BackgroundColor = rowColor;
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CaseReports_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ── Helpers ───────────────────────────────────────────────
        private async Task<string> GenerateCaseId()
        {
            var year  = DateTime.Now.Year;
            var count = await _db.CaseReports.CountAsync(c => c.CreatedAt.Year == year);
            var id    = $"CASE-{year}-{count + 1:D4}";
            // ensure uniqueness in edge cases
            while (await _db.CaseReports.AnyAsync(c => c.CaseId == id))
            {
                count++;
                id = $"CASE-{year}-{count + 1:D4}";
            }
            return id;
        }

        private async Task<string?> SaveFile(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExt.Contains(ext)) return null;
            var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/{folder}/{fileName}";
        }

        private void DeleteFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var full = Path.Combine(_env.WebRootPath, path.TrimStart('/'));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Categories            = CrimeCategories;
            ViewBag.InvestigationStatuses = InvestigationStatuses;
            ViewBag.CaseStatuses          = CaseStatuses;
            ViewBag.Prisons               = new SelectList(
                await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
        }

        private async Task LogActivity(string desc, string type)
        {
            _db.Activities.Add(new Activity { Description = desc, ActivityType = type, UserName = User.Identity?.Name });
            await _db.SaveChangesAsync();
        }
    }
}
