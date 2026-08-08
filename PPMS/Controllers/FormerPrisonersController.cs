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
            var query = _db.FormerPrisoners.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.FullName.Contains(search)   ||
                    p.NationalId.Contains(search) ||
                    p.PrisonerId.Contains(search) ||
                    p.CrimeType.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.ArchiveReason == status);

            if (prisonId.HasValue)
                query = query.Where(p => p.OriginalPrisonId == prisonId.Value);

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

            // Summary counts over full archive
            ViewBag.TotalArchived    = await _db.FormerPrisoners.CountAsync();
            ViewBag.ReleasedCount    = await _db.FormerPrisoners.CountAsync(p => p.ArchiveReason == "Released");
            ViewBag.TransferredCount = await _db.FormerPrisoners.CountAsync(p => p.ArchiveReason == "Transferred");
            ViewBag.DeceasedCount    = await _db.FormerPrisoners.CountAsync(p => p.ArchiveReason == "Deceased");
            ViewBag.AdminCount       = await _db.FormerPrisoners.CountAsync(p => p.ArchiveReason == "Administrative");

            var paginated = await PaginatedList<FormerPrisoner>.CreateAsync(query, page, PageSize);

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
            var former = await _db.FormerPrisoners.FindAsync(id);
            if (former == null) return NotFound();
            return View(former);
        }

        public async Task<IActionResult> PrintRecord(int id)
        {
            var former = await _db.FormerPrisoners.FindAsync(id);
            if (former == null) return NotFound();
            return View(former);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Restore(int id)
        {
            var former = await _db.FormerPrisoners.FindAsync(id);
            if (former == null) return NotFound();

            // Locate the original prison; fall back to matching by name if it was deleted
            Prison? prison = null;
            if (former.OriginalPrisonId.HasValue)
                prison = await _db.Prisons.FindAsync(former.OriginalPrisonId.Value);
            if (prison == null)
                prison = await _db.Prisons.FirstOrDefaultAsync(p => p.PrisonName == former.PrisonName);
            if (prison == null)
            {
                TempData["Error"] = $"Cannot restore: prison '{former.PrisonName}' no longer exists. Recreate it first.";
                return RedirectToAction(nameof(Index));
            }

            // Guard: PrisonerId must not already exist in active records
            if (await _db.Prisoners.AnyAsync(p => p.PrisonerId == former.PrisonerId))
            {
                TempData["Error"] = $"Cannot restore: Prisoner ID '{former.PrisonerId}' already exists in active records.";
                return RedirectToAction(nameof(Index));
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Prisoners.Add(new Prisoner
                {
                    PrisonerId             = former.PrisonerId,
                    FullName               = former.FullName,
                    NationalId             = former.NationalId,
                    Gender                 = former.Gender,
                    DateOfBirth            = former.DateOfBirth,
                    CrimeType              = former.CrimeType,
                    SentenceDurationMonths = former.SentenceDurationMonths,
                    EntryDate              = former.EntryDate,
                    ReleaseDate            = former.ReleaseDate,
                    CriminalStatus         = "Active",
                    Address                = former.Address,
                    EmergencyContact       = former.EmergencyContact,
                    PhotoPath              = former.PhotoPath,
                    FingerprintData        = former.FingerprintData,
                    PrisonId               = prison.Id,
                    CreatedAt              = former.OriginalCreatedAt,
                    UpdatedAt              = DateTime.Now,
                    IsArchived             = false
                });
                _db.FormerPrisoners.Remove(former);
                await _db.SaveChangesAsync();

                _db.Activities.Add(new Activity
                {
                    Description  = $"Former prisoner '{former.FullName}' (ID: {former.PrisonerId}) restored to active records.",
                    ActivityType = "Archive",
                    UserName     = User.Identity?.Name
                });
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "An error occurred while restoring the prisoner. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            await UpdatePrisonPopulation(prison.Id);
            TempData["Success"] = $"'{former.FullName}' has been restored to active prisoner records.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ExportExcel(
            string? search, string? status, int? prisonId, string? from, string? to)
        {
            var query = _db.FormerPrisoners.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.FullName.Contains(search) || p.NationalId.Contains(search) ||
                    p.PrisonerId.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.ArchiveReason == status);

            if (prisonId.HasValue)
                query = query.Where(p => p.OriginalPrisonId == prisonId.Value);

            if (DateTime.TryParse(from, out var fromDt))
                query = query.Where(p => p.ArchivedAt >= fromDt);

            if (DateTime.TryParse(to, out var toDt))
                query = query.Where(p => p.ArchivedAt <= toDt.AddDays(1));

            var data = await query.OrderBy(p => p.FullName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Former Prisoners");

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
                var p = data[i];
                int r = i + 2;
                ws.Cell(r, 1).Value  = p.PrisonerId;
                ws.Cell(r, 2).Value  = p.FullName;
                ws.Cell(r, 3).Value  = p.NationalId;
                ws.Cell(r, 4).Value  = p.Gender;
                ws.Cell(r, 5).Value  = p.DateOfBirth.ToString("yyyy-MM-dd");
                ws.Cell(r, 6).Value  = p.CrimeType;
                ws.Cell(r, 7).Value  = p.SentenceDurationMonths;
                ws.Cell(r, 8).Value  = p.PrisonName;
                ws.Cell(r, 9).Value  = p.EntryDate.ToString("yyyy-MM-dd");
                ws.Cell(r, 10).Value = p.ReleaseDate.ToString("yyyy-MM-dd");
                ws.Cell(r, 11).Value = p.ArchiveReason ?? "Administrative";
                ws.Cell(r, 12).Value = p.ArchivedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                ws.Cell(r, 13).Value = p.ArchivedBy ?? "";
                ws.Cell(r, 14).Value = p.ArchiveNotes ?? "";

                var rowColor = p.ArchiveReason switch
                {
                    "Released"    => XLColor.FromHtml("#f0fdf4"),
                    "Transferred" => XLColor.FromHtml("#eff6ff"),
                    "Deceased"    => XLColor.FromHtml("#f8f8f8"),
                    _             => XLColor.FromHtml("#fffbeb")
                };
                for (int c = 1; c <= headers.Length; c++)
                    ws.Cell(r, c).Style.Fill.BackgroundColor = rowColor;
            }

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
