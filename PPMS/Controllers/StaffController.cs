using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;

namespace PPMS.Controllers
{
    [Authorize(Roles = "SuperAdmin,PrisonAdministrator")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _db;
        private const int PageSize = 15;

        public StaffController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? search, int? prisonId, string? role, int page = 1)
        {
            var query = _db.Staff.Include(s => s.Prison).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.FullName.Contains(search) || s.StaffIdNumber.Contains(search) || s.Email.Contains(search));
            if (prisonId.HasValue)
                query = query.Where(s => s.PrisonId == prisonId.Value);
            if (!string.IsNullOrEmpty(role))
                query = query.Where(s => s.Role == role);

            var paginated = await PaginatedList<Staff>.CreateAsync(query.OrderBy(s => s.FullName), page, PageSize);

            ViewBag.Search = search;
            ViewBag.PrisonId = prisonId;
            ViewBag.Role = role;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
            return View(paginated);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new Staff { IsActive = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Staff model)
        {
            ModelState.Remove("Prison");
            if (await _db.Staff.AnyAsync(s => s.StaffIdNumber == model.StaffIdNumber))
                ModelState.AddModelError("StaffIdNumber", "Staff ID already exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }
            _db.Staff.Add(model);
            await _db.SaveChangesAsync();
            await LogActivity($"Staff '{model.FullName}' (ID: {model.StaffIdNumber}) registered.");
            TempData["Success"] = $"Staff member '{model.FullName}' added successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var staff = await _db.Staff.FindAsync(id);
            if (staff == null) return NotFound();
            await PopulateDropdowns();
            return View(staff);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Staff model)
        {
            if (id != model.Id) return BadRequest();
            ModelState.Remove("Prison");
            if (await _db.Staff.AnyAsync(s => s.StaffIdNumber == model.StaffIdNumber && s.Id != id))
                ModelState.AddModelError("StaffIdNumber", "Another staff with this ID exists.");

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }
            _db.Update(model);
            await _db.SaveChangesAsync();
            await LogActivity($"Staff '{model.FullName}' record updated.");
            TempData["Success"] = "Staff record updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var staff = await _db.Staff.Include(s => s.Prison).FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null) return NotFound();
            return View(staff);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var staff = await _db.Staff.Include(s => s.Prison).FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null) return NotFound();
            return View(staff);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var staff = await _db.Staff.FindAsync(id);
            if (staff == null) return NotFound();
            _db.Staff.Remove(staff);
            await _db.SaveChangesAsync();
            await LogActivity($"Staff '{staff.FullName}' deleted.");
            TempData["Success"] = "Staff record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Prisons = new SelectList(await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
        }

        private async Task LogActivity(string desc)
        {
            _db.Activities.Add(new Activity { Description = desc, ActivityType = "Staff", UserName = User.Identity?.Name });
            await _db.SaveChangesAsync();
        }
    }
}
