using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;

namespace PPMS.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReportsController(ApplicationDbContext db) => _db = db;

        public IActionResult Index() => View();

        public async Task<IActionResult> Prisoners(string? prisonId, string? crimeType, DateTime? from, DateTime? to)
        {
            var query = _db.Prisoners.Include(p => p.Prison).AsQueryable();
            if (!string.IsNullOrEmpty(prisonId) && int.TryParse(prisonId, out int pid))
                query = query.Where(p => p.PrisonId == pid);
            if (!string.IsNullOrEmpty(crimeType))
                query = query.Where(p => p.CrimeType.Contains(crimeType));
            if (from.HasValue) query = query.Where(p => p.EntryDate >= from.Value);
            if (to.HasValue) query = query.Where(p => p.EntryDate <= to.Value);

            ViewBag.Prisons = await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync();
            ViewBag.Selected = new { prisonId, crimeType, from, to };
            return View(await query.OrderBy(p => p.FullName).ToListAsync());
        }

        public async Task<IActionResult> Staff(string? prisonId, string? role)
        {
            var query = _db.Staff.Include(s => s.Prison).AsQueryable();
            if (!string.IsNullOrEmpty(prisonId) && int.TryParse(prisonId, out int pid))
                query = query.Where(s => s.PrisonId == pid);
            if (!string.IsNullOrEmpty(role))
                query = query.Where(s => s.Role.Contains(role));
            ViewBag.Prisons = await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync();
            return View(await query.OrderBy(s => s.FullName).ToListAsync());
        }

        public async Task<IActionResult> Prisons()
        {
            return View(await _db.Prisons.Include(p => p.Prisoners).Include(p => p.Staff).ToListAsync());
        }

        public async Task<IActionResult> WantedCriminals(string? riskLevel)
        {
            var query = _db.WantedCriminals.AsQueryable();
            if (!string.IsNullOrEmpty(riskLevel))
                query = query.Where(w => w.RiskLevel == riskLevel);
            return View(await query.OrderByDescending(w => w.DateAdded).ToListAsync());
        }

        public async Task<IActionResult> ExportPrisonersExcel(string? prisonId, string? crimeType)
        {
            var query = _db.Prisoners.Include(p => p.Prison).AsQueryable();
            if (!string.IsNullOrEmpty(prisonId) && int.TryParse(prisonId, out int pid))
                query = query.Where(p => p.PrisonId == pid);
            if (!string.IsNullOrEmpty(crimeType))
                query = query.Where(p => p.CrimeType.Contains(crimeType));

            var data = await query.OrderBy(p => p.FullName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Prisoners");
            var headers = new[] { "Prisoner ID", "Full Name", "National ID", "Gender", "Date of Birth", "Crime Type", "Sentence (Months)", "Entry Date", "Release Date", "Prison", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0d1b2a");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            for (int i = 0; i < data.Count; i++)
            {
                var p = data[i];
                ws.Cell(i + 2, 1).Value = p.PrisonerId;
                ws.Cell(i + 2, 2).Value = p.FullName;
                ws.Cell(i + 2, 3).Value = p.NationalId;
                ws.Cell(i + 2, 4).Value = p.Gender;
                ws.Cell(i + 2, 5).Value = p.DateOfBirth.ToString("yyyy-MM-dd");
                ws.Cell(i + 2, 6).Value = p.CrimeType;
                ws.Cell(i + 2, 7).Value = p.SentenceDurationMonths;
                ws.Cell(i + 2, 8).Value = p.EntryDate.ToString("yyyy-MM-dd");
                ws.Cell(i + 2, 9).Value = p.ReleaseDate.ToString("yyyy-MM-dd");
                ws.Cell(i + 2, 10).Value = p.Prison?.PrisonName ?? "";
                ws.Cell(i + 2, 11).Value = p.CriminalStatus;
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Prisoners_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> FormerPrisoners(string? status)
        {
            var query = _db.Prisoners.Include(p => p.Prison).Where(p => p.IsArchived).AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.ArchiveReason == status);
            ViewBag.Status = status;
            return View(await query.OrderByDescending(p => p.ArchivedAt).ToListAsync());
        }

        public async Task<IActionResult> ExportFormerPrisonersExcel(string? status)
        {
            var query = _db.Prisoners.Include(p => p.Prison).Where(p => p.IsArchived).AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.ArchiveReason == status);

            var data = await query.OrderBy(p => p.FullName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Former Prisoners");
            var headers = new[] { "Prisoner ID", "Full Name", "National ID", "Gender", "Crime Type", "Sentence (Months)", "Prison", "Entry Date", "Release Date", "Archive Status", "Archived Date", "Archived By" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0d1b2a");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            for (int i = 0; i < data.Count; i++)
            {
                var p = data[i];
                ws.Cell(i + 2, 1).Value  = p.PrisonerId;
                ws.Cell(i + 2, 2).Value  = p.FullName;
                ws.Cell(i + 2, 3).Value  = p.NationalId;
                ws.Cell(i + 2, 4).Value  = p.Gender;
                ws.Cell(i + 2, 5).Value  = p.CrimeType;
                ws.Cell(i + 2, 6).Value  = p.SentenceDurationMonths;
                ws.Cell(i + 2, 7).Value  = p.Prison?.PrisonName ?? "";
                ws.Cell(i + 2, 8).Value  = p.EntryDate.ToString("yyyy-MM-dd");
                ws.Cell(i + 2, 9).Value  = p.ReleaseDate.ToString("yyyy-MM-dd");
                ws.Cell(i + 2, 10).Value = p.ArchiveReason ?? "Administrative";
                ws.Cell(i + 2, 11).Value = p.ArchivedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                ws.Cell(i + 2, 12).Value = p.ArchivedBy ?? "";
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"FormerPrisoners_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> Security()
        {
            ViewBag.ActiveAlerts       = await _db.Alerts.CountAsync(a => a.IsActive && !a.IsDismissed);
            ViewBag.TotalAlerts        = await _db.Alerts.CountAsync();
            ViewBag.HighRiskPrisoners  = await _db.Prisoners.CountAsync(p => !p.IsArchived && p.CriminalStatus == "Active" &&
                                             (p.CrimeType.Contains("Murder") || p.CrimeType.Contains("Terrorism") || p.CrimeType.Contains("Drug")));
            ViewBag.TotalWanted        = await _db.WantedCriminals.CountAsync(w => w.IsActive);
            ViewBag.CriticalWanted     = await _db.WantedCriminals.CountAsync(w => w.IsActive && w.RiskLevel == "Critical");
            ViewBag.HighWanted         = await _db.WantedCriminals.CountAsync(w => w.IsActive && w.RiskLevel == "High");
            ViewBag.MediumWanted       = await _db.WantedCriminals.CountAsync(w => w.IsActive && w.RiskLevel == "Medium");
            ViewBag.LowWanted          = await _db.WantedCriminals.CountAsync(w => w.IsActive && w.RiskLevel == "Low");
            ViewBag.TotalPrisoners     = await _db.Prisoners.CountAsync(p => !p.IsArchived);
            ViewBag.TotalPrisons       = await _db.Prisons.CountAsync();

            ViewBag.RecentAlerts = await _db.Alerts
                .Where(a => !a.IsDismissed)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.WantedByCritical = await _db.WantedCriminals
                .Where(w => w.IsActive && w.RiskLevel == "Critical")
                .OrderByDescending(a => a.DateAdded)
                .Take(10)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> ExportWantedCriminalsExcel(string? riskLevel)
        {
            var query = _db.WantedCriminals.AsQueryable();
            if (!string.IsNullOrEmpty(riskLevel))
                query = query.Where(w => w.RiskLevel == riskLevel);

            var data = await query.OrderBy(w => w.CriminalName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Wanted Criminals");
            var headers = new[] { "Full Name", "National ID", "Crime Description", "Risk Level", "Last Known Location", "Date Added" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#7f0000");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            for (int i = 0; i < data.Count; i++)
            {
                var w = data[i];
                ws.Cell(i + 2, 1).Value = w.CriminalName;
                ws.Cell(i + 2, 2).Value = w.NationalId ?? "";
                ws.Cell(i + 2, 3).Value = w.CrimeDescription;
                ws.Cell(i + 2, 4).Value = w.RiskLevel;
                ws.Cell(i + 2, 5).Value = w.LastKnownLocation ?? "";
                ws.Cell(i + 2, 6).Value = w.DateAdded.ToString("yyyy-MM-dd");
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"WantedCriminals_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ExportSecurityExcel()
        {
            var alerts  = await _db.Alerts.Where(a => !a.IsDismissed).OrderByDescending(a => a.CreatedAt).ToListAsync();
            var wanted  = await _db.WantedCriminals.Where(w => w.IsActive).OrderBy(w => w.RiskLevel).ToListAsync();

            using var wb = new XLWorkbook();

            // Sheet 1 — Alerts
            var ws1 = wb.Worksheets.Add("Active Alerts");
            var ah = new[] { "Alert Type", "Message", "Severity", "Created At", "Created By" };
            for (int i = 0; i < ah.Length; i++) { ws1.Cell(1, i + 1).Value = ah[i]; ws1.Cell(1, i + 1).Style.Font.Bold = true; ws1.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4a1d96"); ws1.Cell(1, i + 1).Style.Font.FontColor = XLColor.White; }
            for (int i = 0; i < alerts.Count; i++) { ws1.Cell(i + 2, 1).Value = alerts[i].AlertType; ws1.Cell(i + 2, 2).Value = alerts[i].Message; ws1.Cell(i + 2, 3).Value = alerts[i].Severity; ws1.Cell(i + 2, 4).Value = alerts[i].CreatedAt.ToString("yyyy-MM-dd HH:mm"); ws1.Cell(i + 2, 5).Value = alerts[i].CreatedBy ?? ""; }
            ws1.Columns().AdjustToContents();

            // Sheet 2 — Wanted
            var ws2 = wb.Worksheets.Add("Wanted Criminals");
            var wh = new[] { "Full Name", "National ID", "Risk Level", "Crime Description", "Last Known Location", "Date Added" };
            for (int i = 0; i < wh.Length; i++) { ws2.Cell(1, i + 1).Value = wh[i]; ws2.Cell(1, i + 1).Style.Font.Bold = true; ws2.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#7f0000"); ws2.Cell(1, i + 1).Style.Font.FontColor = XLColor.White; }
            for (int i = 0; i < wanted.Count; i++) { ws2.Cell(i + 2, 1).Value = wanted[i].CriminalName; ws2.Cell(i + 2, 2).Value = wanted[i].NationalId ?? ""; ws2.Cell(i + 2, 3).Value = wanted[i].RiskLevel; ws2.Cell(i + 2, 4).Value = wanted[i].CrimeDescription; ws2.Cell(i + 2, 5).Value = wanted[i].LastKnownLocation ?? ""; ws2.Cell(i + 2, 6).Value = wanted[i].DateAdded.ToString("yyyy-MM-dd"); }
            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SecurityReport_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ExportStaffExcel()
        {
            var data = await _db.Staff.Include(s => s.Prison).OrderBy(s => s.FullName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Staff");
            var headers = new[] { "Staff ID", "Full Name", "Phone", "Email", "Gender", "Role", "Prison", "Shift", "Active" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0d1b2a");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            for (int i = 0; i < data.Count; i++)
            {
                var s = data[i];
                ws.Cell(i + 2, 1).Value = s.StaffIdNumber;
                ws.Cell(i + 2, 2).Value = s.FullName;
                ws.Cell(i + 2, 3).Value = s.PhoneNumber;
                ws.Cell(i + 2, 4).Value = s.Email;
                ws.Cell(i + 2, 5).Value = s.Gender;
                ws.Cell(i + 2, 6).Value = s.Role;
                ws.Cell(i + 2, 7).Value = s.Prison?.PrisonName ?? "";
                ws.Cell(i + 2, 8).Value = s.ShiftSchedule;
                ws.Cell(i + 2, 9).Value = s.IsActive ? "Yes" : "No";
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Staff_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }
}
