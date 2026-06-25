using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;

namespace PPMS.Controllers
{
    [Authorize]
    public class PrisonsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private const int PageSize = 12;

        public PrisonsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? search, string? security, int page = 1)
        {
            var query = _db.Prisons.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.PrisonName.Contains(search) || p.City.Contains(search));
            if (!string.IsNullOrEmpty(security))
                query = query.Where(p => p.SecurityLevel == security);

            var paginated = await PaginatedList<Prison>.CreateAsync(query.OrderBy(p => p.PrisonName), page, PageSize);

            ViewBag.Search = search;
            ViewBag.Security = security;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            return View(paginated);
        }

        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Prison model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Prisons.Add(model);
            await _db.SaveChangesAsync();
            await LogActivity($"Prison '{model.PrisonName}' added.");
            TempData["Success"] = "Prison added successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var prison = await _db.Prisons.FindAsync(id);
            if (prison == null) return NotFound();
            return View(prison);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Prison model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);
            _db.Update(model);
            await _db.SaveChangesAsync();
            await LogActivity($"Prison '{model.PrisonName}' updated.");
            TempData["Success"] = "Prison updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var prison = await _db.Prisons
                .Include(p => p.Prisoners)
                .Include(p => p.Staff)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (prison == null) return NotFound();
            return View(prison);
        }

        [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var prison = await _db.Prisons.FindAsync(id);
            if (prison == null) return NotFound();
            return View(prison);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prison = await _db.Prisons.FindAsync(id);
            if (prison == null) return NotFound();
            _db.Prisons.Remove(prison);
            await _db.SaveChangesAsync();
            await LogActivity($"Prison '{prison.PrisonName}' deleted.");
            TempData["Success"] = "Prison deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LogActivity(string desc)
        {
            _db.Activities.Add(new Activity
            {
                Description = desc,
                ActivityType = "Prison",
                UserName = User.Identity?.Name
            });
            await _db.SaveChangesAsync();
        }
    }
}
