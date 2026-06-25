using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PPMS.Models;

namespace PPMS.ViewComponents
{
    [ViewComponent(Name = "UserProfile")]
    public class UserProfileViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserProfileViewComponent(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (user == null) return Content(string.Empty);

            return View(user);
        }
    }
}
