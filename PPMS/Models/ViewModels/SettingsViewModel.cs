using System.ComponentModel.DataAnnotations;

namespace PPMS.Models.ViewModels
{
    public class SettingsViewModel
    {
        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [EmailAddress, MaxLength(200)]
        public string? Email { get; set; }

        [Phone, MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public string Theme { get; set; } = "light";

        public string? ProfilePhotoPath { get; set; }

        public string? PrisonAssigned { get; set; }

        public string Role { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string? LastLoginIp { get; set; }

        public int LoginCount { get; set; }
    }
}
