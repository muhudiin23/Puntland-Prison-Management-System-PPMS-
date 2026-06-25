using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;

namespace PPMS.Controllers
{
    [Authorize]
    public class AlertsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private const int PageSize = 20;

        public AlertsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(bool showAll = false, string? alertType = null, int page = 1)
        {
            var query = _db.Alerts.AsQueryable();
            if (!showAll) query = query.Where(a => !a.IsDismissed);
            if (!string.IsNullOrEmpty(alertType)) query = query.Where(a => a.AlertType == alertType);

            var paginated = await PaginatedList<Alert>.CreateAsync(query.OrderByDescending(a => a.CreatedAt), page, PageSize);

            ViewBag.ShowAll = showAll;
            ViewBag.AlertType = alertType;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            ViewBag.ActiveCount = await _db.Alerts.CountAsync(a => !a.IsDismissed);
            return View(paginated);
        }

        public IActionResult Create() => View(new Alert { IsActive = true });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Alert model)
        {
            if (!ModelState.IsValid) return View(model);
            model.CreatedBy = User.Identity?.Name;
            model.IsActive = true;
            _db.Alerts.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Alert created and broadcast.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id)
        {
            var alert = await _db.Alerts.FindAsync(id);
            if (alert == null) return NotFound();
            alert.IsDismissed = true;
            alert.IsActive = false;
            alert.DismissedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Alert dismissed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissAll()
        {
            var alerts = await _db.Alerts.Where(a => !a.IsDismissed).ToListAsync();
            foreach (var a in alerts) { a.IsDismissed = true; a.IsActive = false; a.DismissedAt = DateTime.Now; }
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{alerts.Count} alerts dismissed.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> History(int page = 1)
        {
            var query = _db.Alerts.Where(a => a.IsDismissed).OrderByDescending(a => a.DismissedAt);
            var paginated = await PaginatedList<Alert>.CreateAsync(query, page, PageSize);
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            return View(paginated);
        }
    }
}
