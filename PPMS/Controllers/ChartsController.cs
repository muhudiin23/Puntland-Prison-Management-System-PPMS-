using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;

namespace PPMS.Controllers
{
    [Authorize]
    [Route("api/charts")]
    public class ChartsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ChartsController(ApplicationDbContext db) => _db = db;

        [HttpGet("monthly-registrations")]
        public async Task<IActionResult> MonthlyRegistrations()
        {
            var now = DateTime.Now;
            var data = new List<object>();

            for (int i = 11; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var count = await _db.Prisoners.CountAsync(p =>
                    p.CreatedAt.Year == month.Year && p.CreatedAt.Month == month.Month);
                data.Add(new { label = month.ToString("MMM yyyy"), count });
            }

            return Ok(data);
        }

        [HttpGet("criminal-status")]
        public async Task<IActionResult> CriminalStatus()
        {
            var statuses = await _db.Prisoners
                .GroupBy(p => p.CriminalStatus)
                .Select(g => new { label = g.Key, count = g.Count() })
                .ToListAsync();
            return Ok(statuses);
        }

        [HttpGet("prison-occupancy")]
        public async Task<IActionResult> PrisonOccupancy()
        {
            var data = await _db.Prisons
                .Select(p => new
                {
                    label = p.PrisonName,
                    capacity = p.Capacity,
                    population = p.CurrentPopulation
                })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("crime-types")]
        public async Task<IActionResult> CrimeTypes()
        {
            var data = await _db.Prisoners
                .GroupBy(p => p.CrimeType)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(8)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("staff-by-role")]
        public async Task<IActionResult> StaffByRole()
        {
            var data = await _db.Staff
                .GroupBy(s => s.Role)
                .Select(g => new { label = g.Key, count = g.Count() })
                .ToListAsync();
            return Ok(data);
        }

        // ── Case Report analytics ──────────────────────────────────

        [HttpGet("cases-by-type")]
        public async Task<IActionResult> CasesByType()
        {
            var data = await _db.CaseReports
                .GroupBy(c => c.CrimeType)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("cases-by-status")]
        public async Task<IActionResult> CasesByStatus()
        {
            var data = await _db.CaseReports
                .GroupBy(c => c.CaseStatus)
                .Select(g => new { label = g.Key, count = g.Count() })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("cases-monthly")]
        public async Task<IActionResult> CasesMonthly()
        {
            var now  = DateTime.Now;
            var data = new List<object>();
            for (int i = 11; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var count = await _db.CaseReports.CountAsync(c =>
                    c.DateOfCrime.Year == month.Year && c.DateOfCrime.Month == month.Month);
                data.Add(new { label = month.ToString("MMM yyyy"), count });
            }
            return Ok(data);
        }

        [HttpGet("cases-by-prison")]
        public async Task<IActionResult> CasesByPrison()
        {
            var data = await _db.CaseReports
                .Where(c => c.PrisonId != null)
                .Include(c => c.Prison)
                .GroupBy(c => c.Prison!.PrisonName)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("cases-by-year")]
        public async Task<IActionResult> CasesByYear()
        {
            var data = await _db.CaseReports
                .GroupBy(c => c.DateOfCrime.Year)
                .Select(g => new { label = g.Key.ToString(), count = g.Count() })
                .OrderBy(x => x.label)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("cases-investigation")]
        public async Task<IActionResult> CasesInvestigation()
        {
            var data = await _db.CaseReports
                .GroupBy(c => c.InvestigationStatus)
                .Select(g => new { label = g.Key, count = g.Count() })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications()
        {
            var alerts = await _db.Alerts
                .Where(a => !a.IsDismissed && a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new { a.Id, a.AlertType, a.Message, a.Severity, a.CreatedAt })
                .ToListAsync();
            return Ok(new { count = alerts.Count, items = alerts });
        }
    }
}
