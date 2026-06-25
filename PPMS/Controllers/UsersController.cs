using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Helpers;
using PPMS.Models;
using PPMS.Models.ViewModels;

namespace PPMS.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private const int PageSize = 15;

        public UsersController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        public async Task<IActionResult> Index(string? search, string? role, int page = 1)
        {
            var query = _userManager.Users.AsQueryable();
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

        public IActionResult Create() => View(new UserManageViewModel { IsActive = true });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserManageViewModel model)
        {
            if (string.IsNullOrEmpty(model.Password))
                ModelState.AddModelError("Password", "Password is required.");
            if (!ModelState.IsValid) return View(model);

            if (await _userManager.FindByNameAsync(model.Username) != null)
            {
                ModelState.AddModelError("Username", "Username already taken.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                Role = model.Role,
                PrisonAssigned = model.PrisonAssigned,
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
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(new UserManageViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.UserName ?? "",
                Email = user.Email,
                Role = user.Role,
                PrisonAssigned = user.PrisonAssigned,
                IsActive = user.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserManageViewModel model)
        {
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Role = model.Role;
            user.PrisonAssigned = model.PrisonAssigned;
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
        public async Task<IActionResult> ResetPassword(string id, string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "Password must be at least 8 characters.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "Password reset successfully."
                : string.Join(", ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }
    }
}
