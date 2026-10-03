using System.Security.Claims;

namespace PPMS.Helpers
{
    public static class PrisonAccessHelper
    {
        public const string PrisonIdClaim = "AssignedPrisonId";

        public static int? PrisonId(this ClaimsPrincipal user)
        {
            var v = user.FindFirstValue(PrisonIdClaim);
            return int.TryParse(v, out var id) ? id : null;
        }

        public static bool IsSuperAdmin(this ClaimsPrincipal user)
            => user.IsInRole("SuperAdmin");
    }
}
