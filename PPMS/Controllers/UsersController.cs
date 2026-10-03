using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;
using PPMS.Models;
using PPMS.Models.ViewModels;

namespace PPMS.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private const int PageSize = 15;

        public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        public async Task<IActionResult> Index(string? search, string? role, int page = 1)
        {
            var query = _db.Users.Include(u => u.AssignedPrison).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.FullName.Contains(search) || (u.UserName != null && u.UserName.Contains(search)) || (u.Email != null && u.Email.Contains(search)));
            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.Role == role);

            var paginated = await PaginatedList<ApplicationUser>.CreateAsync(query.OrderBy(u => u.FullName), page, PageSize);

            ViewBag.Search = search;
            ViewBag.Role = role;
            ViewBag.PageIndex = paginated.PageIndex;
            ViewBag.TotalPages = paginated.TotalPages;
            ViewBag.TotalCount = paginated.TotalCount;
            return View(paginated);
        }

        public async Task<IActionResult> Create()
        {
            await PopulatePrisons();
            return View(new UserManageViewModel { IsActive = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserManageViewModel model)
        {
            if (string.IsNullOrEmpty(model.Password))
                ModelState.AddModelError("Password", "Password is required.");
            if (!ModelState.IsValid) { await PopulatePrisons(); return View(model); }

            if (await _userManager.FindByNameAsync(model.Username) != null)
            {
                ModelState.AddModelError("Username", "Username already taken.");
                await PopulatePrisons();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                Role = model.Role,
                AssignedPrisonId = model.AssignedPrisonId,
                IsActive = model.IsActive,
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(user, model.Password!);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                TempData["Success"] = $"User '{model.Username}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            await PopulatePrisons();
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await PopulatePrisons();
            return View(new UserManageViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.UserName ?? "",
                Email = user.Email,
                Role = user.Role,
                AssignedPrisonId = user.AssignedPrisonId,
                IsActive = user.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserManageViewModel model)
        {
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");
            if (!ModelState.IsValid) { await PopulatePrisons(); return View(model); }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.NormalizedEmail = model.Email?.ToUpperInvariant();
            user.Role = model.Role;
            user.AssignedPrisonId = model.AssignedPrisonId;
            user.IsActive = model.IsActive;

            await _userManager.UpdateAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, roles);
            await _userManager.AddToRoleAsync(user, model.Role);

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = user.IsActive ? "User account enabled." : "User account disabled.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword, string confirmPassword, bool fromIndex = false)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "Password must be at least 8 characters and contain at least one digit.";
                return fromIndex ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Edit), new { id });
            }
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return fromIndex ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Edit), new { id });
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? $"Password for '{user.UserName}' reset successfully."
                : string.Join(", ", result.Errors.Select(e => e.Description));
            return fromIndex ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Edit), new { id });
        }

        private async Task PopulatePrisons()
        {
            ViewBag.Prisons = new SelectList(
                await _db.Prisons.OrderBy(p => p.PrisonName).ToListAsync(), "Id", "PrisonName");
        }
    }
}
