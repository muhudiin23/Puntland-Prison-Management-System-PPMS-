using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PPMS.Data;
using PPMS.Models;
using PPMS.Models.ViewModels;

namespace PPMS.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public SettingsController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            return View(new SettingsViewModel
            {
                FullName = user.FullName,
                Username = user.UserName ?? "",
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Theme = user.Theme,
                ProfilePhotoPath = user.ProfilePhotoPath,
                PrisonAssigned = user.PrisonAssigned,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                LastLoginIp = user.LastLoginIp,
                LoginCount = user.LoginCount
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(SettingsViewModel model, IFormFile? profilePhoto)
        {
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");
            ModelState.Remove("CurrentPassword");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!string.IsNullOrEmpty(model.Username) && model.Username != user.UserName)
            {
                if (await _userManager.FindByNameAsync(model.Username) != null)
                {
                    TempData["Error"] = "That username is already taken.";
                    return RedirectToAction(nameof(Index));
                }
                user.UserName = model.Username;
                user.NormalizedUserName = model.Username.ToUpperInvariant();
            }

            if (!string.IsNullOrEmpty(model.Email) && model.Email != user.Email)
            {
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    TempData["Error"] = "That email is already in use.";
                    return RedirectToAction(nameof(Index));
                }
                user.Email = model.Email;
                user.NormalizedEmail = model.Email.ToUpperInvariant();
            }

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            if (profilePhoto != null && profilePhoto.Length > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(profilePhoto.FileName).ToLower();
                if (!allowedExt.Contains(ext))
                {
                    TempData["Error"] = "Invalid photo format. Use JPG, PNG, or GIF.";
                    return RedirectToAction(nameof(Index));
                }
                if (profilePhoto.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Photo must be under 5MB.";
                    return RedirectToAction(nameof(Index));
                }

                var dir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(dir);

                if (!string.IsNullOrEmpty(user.ProfilePhotoPath))
                {
                    var old = Path.Combine(_env.WebRootPath, user.ProfilePhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                }

                var fileName = $"profile_{user.Id}{ext}";
                using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
                await profilePhoto.CopyToAsync(stream);
                user.ProfilePhotoPath = $"/uploads/profiles/{fileName}";
            }

            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            await LogActivity("Profile settings updated.");
            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                await LogActivity("Password changed.");
                TempData["Success"] = "Password changed successfully.";
            }
            else
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTheme(string theme)
        {
            if (theme != "light" && theme != "dark") theme = "light";

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.Theme = theme;
                await _userManager.UpdateAsync(user);
                await _signInManager.RefreshSignInAsync(user);
            }

            Response.Cookies.Append("ppms_theme", theme, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

            return Ok(new { success = true, theme });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.IsNullOrEmpty(user.ProfilePhotoPath))
            {
                var path = Path.Combine(_env.WebRootPath, user.ProfilePhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                user.ProfilePhotoPath = null;
                await _userManager.UpdateAsync(user);
                await _signInManager.RefreshSignInAsync(user);
            }
            TempData["Success"] = "Profile photo removed.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LogActivity(string desc)
        {
            _db.Activities.Add(new Activity
            {
                Description = desc,
                ActivityType = "Settings",
                UserName = User.Identity?.Name
            });
            await _db.SaveChangesAsync();
        }
    }
}
