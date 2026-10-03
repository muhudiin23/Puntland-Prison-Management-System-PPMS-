using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
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
            var superAdmin = User.IsSuperAdmin();
            var prisonId = User.PrisonId();

            string? prisonName = null;
            if (!superAdmin && prisonId.HasValue)
            {
                var prison = await _db.Prisons.FindAsync(prisonId.Value);
                prisonName = prison?.PrisonName;
            }

            var vm = new DashboardViewModel
            {
                IsSuperAdmin = superAdmin,
                AssignedPrisonName = prisonName,
                TotalPrisons = await _db.Prisons.CountAsync(),
                WantedCriminals = await _db.WantedCriminals.CountAsync(w => w.IsActive),
                PendingTransfers = await _db.PrisonerTransfers.CountAsync(t => t.Status == "Pending")
            };

            if (superAdmin)
            {
                vm.TotalPrisoners = await _db.Prisoners.CountAsync(p => !p.IsArchived);
                vm.TotalStaff = await _db.Staff.CountAsync();
                vm.ActiveAlerts = await _db.Alerts.CountAsync(a => a.IsActive && !a.IsDismissed);
                vm.FormerPrisonersCount = await _db.FormerPrisoners.CountAsync();
                vm.TotalCaseReports = await _db.CaseReports.CountAsync();
                vm.OpenCaseReports = await _db.CaseReports.CountAsync(c => c.CaseStatus == "Open");
                vm.PrisonersReleasedThisMonth = await _db.Prisoners.CountAsync(p =>
                    !p.IsArchived && p.ReleaseDate.Month == now.Month && p.ReleaseDate.Year == now.Year);
                vm.RecentAlerts = await _db.Alerts
                    .Where(a => !a.IsDismissed)
                    .OrderByDescending(a => a.CreatedAt).Take(6).ToListAsync();
                vm.RecentActivities = await _db.Activities
                    .OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync();
            }
            else if (prisonId.HasValue)
            {
                vm.TotalPrisoners = await _db.Prisoners.CountAsync(p => p.PrisonId == prisonId && !p.IsArchived);
                vm.TotalStaff = await _db.Staff.CountAsync(s => s.PrisonId == prisonId);
                vm.ActiveAlerts = await _db.Alerts.CountAsync(a => a.IsActive && !a.IsDismissed && a.PrisonId == prisonId);
                vm.FormerPrisonersCount = await _db.FormerPrisoners.CountAsync(p => p.OriginalPrisonId == prisonId);
                vm.TotalCaseReports = await _db.CaseReports.CountAsync(c => c.PrisonId == prisonId);
                vm.OpenCaseReports = await _db.CaseReports.CountAsync(c => c.PrisonId == prisonId && c.CaseStatus == "Open");
                vm.PrisonersReleasedThisMonth = await _db.Prisoners.CountAsync(p =>
                    p.PrisonId == prisonId && !p.IsArchived &&
                    p.ReleaseDate.Month == now.Month && p.ReleaseDate.Year == now.Year);
                vm.RecentAlerts = await _db.Alerts
                    .Where(a => !a.IsDismissed && a.PrisonId == prisonId)
                    .OrderByDescending(a => a.CreatedAt).Take(6).ToListAsync();
                vm.RecentActivities = await _db.Activities
                    .Where(a => a.PrisonId == prisonId)
                    .OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync();
            }

            return View(vm);
        }
    }
}
