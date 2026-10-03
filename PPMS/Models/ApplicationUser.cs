using Microsoft.AspNetCore.Identity;

namespace PPMS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "StaffUser";
        public string? PrisonAssigned { get; set; }
        public int? AssignedPrisonId { get; set; }
        public Prison? AssignedPrison { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? ProfilePhotoPath { get; set; }
        public string Theme { get; set; } = "light";
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIp { get; set; }
        public int LoginCount { get; set; }
    }
}
