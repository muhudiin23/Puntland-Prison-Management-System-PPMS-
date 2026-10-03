using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Models;

namespace PPMS.ViewComponents
{
    [ViewComponent(Name = "UserProfile")]
    public class UserProfileViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public UserProfileViewComponent(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var userId = _userManager.GetUserId(UserClaimsPrincipal);
            if (userId == null) return Content(string.Empty);

            var user = await _db.Users
                .Include(u => u.AssignedPrison)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Content(string.Empty);

            return View(user);
        }
    }
}
