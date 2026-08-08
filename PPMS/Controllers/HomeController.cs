using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Models.ViewModels;

namespace PPMS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var vm = new DashboardViewModel
            {
                TotalPrisoners             = await _db.Prisoners.CountAsync(p => !p.IsArchived),
                TotalStaff                 = await _db.Staff.CountAsync(),
                TotalPrisons               = await _db.Prisons.CountAsync(),
                WantedCriminals            = await _db.WantedCriminals.CountAsync(w => w.IsActive),
                ActiveAlerts               = await _db.Alerts.CountAsync(a => a.IsActive && !a.IsDismissed),
                FormerPrisonersCount       = await _db.FormerPrisoners.CountAsync(),
                TotalCaseReports           = await _db.CaseReports.CountAsync(),
                OpenCaseReports            = await _db.CaseReports.CountAsync(c => c.CaseStatus == "Open"),
                PrisonersReleasedThisMonth = await _db.Prisoners.CountAsync(p =>
                    !p.IsArchived &&
                    p.ReleaseDate.Month == now.Month &&
                    p.ReleaseDate.Year  == now.Year),
                RecentAlerts = await _db.Alerts
                    .Where(a => !a.IsDismissed)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(6)
                    .ToListAsync(),
                RecentActivities = await _db.Activities
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(10)
                    .ToListAsync()
            };
            return View(vm);
        }
    }
}
