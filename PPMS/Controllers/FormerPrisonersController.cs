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
    public class FormerPrisonersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private const int PageSize = 20;

        public FormerPrisonersController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(
            string? search, string? status, int? prisonId,
            string? from, string? to, string? sort, int page = 1)
        {
            var query = _db.Prisoners.Include(p => p.Prison)
                .Where(p => p.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.FullName.Contains(search)   ||
                    p.NationalId.Contains(search) ||
                    p.PrisonerId.Contains(search) ||
                    p.CrimeType.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.ArchiveReason == status);

            if (prisonId.HasValue)
                query = query.Where(p => p.PrisonId == prisonId.Value);

            if (DateTime.TryParse(from, out var fromDt))
                query = query.Where(p => p.ArchivedAt >= fromDt);

            if (DateTime.TryParse(to, out var toDt))
                query = query.Where(p => p.ArchivedAt <= toDt.AddDays(1));

            query = sort switch
            {
                "name"      => query.OrderBy(p => p.FullName),
                "name_desc" => query.OrderByDescending(p => p.FullName),
                "entry"     => query.OrderBy(p => p.EntryDate),
                "release"   => query.OrderBy(p => p.ReleaseDate),
                "archived"  => query.OrderBy(p => p.ArchivedAt),
                _           => query.OrderByDescending(p => p.ArchivedAt)
            };

            // Summary counts (always over full archive regardless of current filters)
            var archive = _db.Prisoners.Where(p => p.IsArchived);
            ViewBag.TotalArchived     = await archive.CountAsync();
            ViewBag.ReleasedCount     = await archive.CountAsync(p => p.ArchiveReason == "Released");
            ViewBag.TransferredCount  = await archive.CountAsync(p => p.ArchiveReason == "Transferred");
            ViewBag.DeceasedCount     = await archive.CountAsync(p => p.ArchiveReason == "Deceased");
            ViewBag.AdminCount        = await archive.CountAsync(p => p.ArchiveReason == "Administrative");

            var paginated = await PaginatedList<Prisoner>.CreateAsync(query, page, PageSize);

            ViewBag.Search     = search;
            ViewBag.Status     = status;
            ViewBag.PrisonId   = prisonId;
            ViewBag.From       = from;
            ViewBag.To         = to;
            ViewBag.Sort       = sort;
            ViewBag.PageIndex  = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            ViewBag.Prisons    = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");

            return View(paginated);
        }

        public async Task<IActionResult> Details(int id)
        {
            var prisoner = await _db.Prisoners.Include(p => p.Prison)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsArchived);
            if (prisoner == null) return NotFound();
            return View(prisoner);
        }

        public async Task<IActionResult> PrintRecord(int id)
        {
            var prisoner = await _db.Prisoners.Include(p => p.Prison)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsArchived);
            if (prisoner == null) return NotFound();
            return View(prisoner);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Restore(int id)
        {
            var prisoner = await _db.Prisoners.FindAsync(id);
            if (prisoner == null || !prisoner.IsArchived) return NotFound();

            prisoner.IsArchived    = false;
            prisoner.ArchivedAt    = null;
            prisoner.ArchiveReason = null;
            prisoner.ArchivedBy    = null;
            prisoner.ArchiveNotes  = null;
            prisoner.CriminalStatus = "Active";
            prisoner.UpdatedAt     = DateTime.Now;

            await _db.SaveChangesAsync();
            await UpdatePrisonPopulation(prisoner.PrisonId);

            _db.Activities.Add(new Activity
            {
                Description  = $"Prisoner '{prisoner.FullName}' (ID: {prisoner.PrisonerId}) restored from archive to active records.",
                ActivityType = "Archive",
                UserName     = User.Identity?.Name
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = $"'{prisoner.FullName}' has been restored to active prisoner records.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ExportExcel(
            string? search, string? status, int? prisonId, string? from, string? to)
        {
            var query = _db.Prisoners.Include(p => p.Prison)
                .Where(p => p.IsArchived).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.FullName.Contains(search) || p.NationalId.Contains(search) ||
                    p.PrisonerId.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.ArchiveReason == status);

            if (prisonId.HasValue)
                query = query.Where(p => p.PrisonId == prisonId.Value);

            if (DateTime.TryParse(from, out var fromDt))
                query = query.Where(p => p.ArchivedAt >= fromDt);

            if (DateTime.TryParse(to, out var toDt))
                query = query.Where(p => p.ArchivedAt <= toDt.AddDays(1));

            var data = await query.OrderBy(p => p.FullName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Former Prisoners");

            // Header styling
            var headers = new[]
            {
                "Prisoner ID", "Full Name", "National ID", "Gender", "Date of Birth",
                "Crime Type", "Sentence (Months)", "Prison", "Entry Date", "Release Date",
                "Archive Status", "Archived Date", "Archived By", "Notes"
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
                var p  = data[i];
                int r  = i + 2;
                ws.Cell(r, 1).Value  = p.PrisonerId;
                ws.Cell(r, 2).Value  = p.FullName;
                ws.Cell(r, 3).Value  = p.NationalId;
                ws.Cell(r, 4).Value  = p.Gender;
                ws.Cell(r, 5).Value  = p.DateOfBirth.ToString("yyyy-MM-dd");
                ws.Cell(r, 6).Value  = p.CrimeType;
                ws.Cell(r, 7).Value  = p.SentenceDurationMonths;
                ws.Cell(r, 8).Value  = p.Prison?.PrisonName ?? "";
                ws.Cell(r, 9).Value  = p.EntryDate.ToString("yyyy-MM-dd");
                ws.Cell(r, 10).Value = p.ReleaseDate.ToString("yyyy-MM-dd");
                ws.Cell(r, 11).Value = p.ArchiveReason ?? "Administrative";
                ws.Cell(r, 12).Value = p.ArchivedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                ws.Cell(r, 13).Value = p.ArchivedBy ?? "";
                ws.Cell(r, 14).Value = p.ArchiveNotes ?? "";

                // Row shading by status
                var rowColor = p.ArchiveReason switch
                {
                    "Released"      => XLColor.FromHtml("#f0fdf4"),
                    "Transferred"   => XLColor.FromHtml("#eff6ff"),
                    "Deceased"      => XLColor.FromHtml("#f8f8f8"),
                    _               => XLColor.FromHtml("#fffbeb")
                };
                for (int c = 1; c <= headers.Length; c++)
                    ws.Cell(r, c).Style.Fill.BackgroundColor = rowColor;
            }

            // Legend row
            int legendRow = data.Count + 3;
            ws.Cell(legendRow, 1).Value = "Legend:";
            ws.Cell(legendRow, 1).Style.Font.Bold = true;
            ws.Cell(legendRow + 1, 1).Value = "Released = Sentence completed";
            ws.Cell(legendRow + 2, 1).Value = "Transferred = Moved to another facility";
            ws.Cell(legendRow + 3, 1).Value = "Deceased = Prisoner deceased";
            ws.Cell(legendRow + 4, 1).Value = "Administrative = Admin action";

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"FormerPrisoners_{DateTime.Now:yyyyMMdd}.xlsx");
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
    }
}
