using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PPMS.Models.ViewModels
{
    public class CreatePrisonViewModel
    {
        // ── Prison Information ────────────────────────────────
        [Required, MaxLength(150)]
        public string PrisonName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, Range(1, 10000, ErrorMessage = "Capacity must be at least 1.")]
        public int Capacity { get; set; }

        [Range(0, 10000)]
        public int CurrentPopulation { get; set; }

        [Required]
        public string SecurityLevel { get; set; } = "Medium";

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(20)]
        public string? ContactNumber { get; set; }

        // ── Prison Administrator Account ──────────────────────
        [Required(ErrorMessage = "Administrator full name is required.")]
        public string AdminFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required.")]
        public string AdminUsername { get; set; } = string.Empty;

        [EmailAddress]
        public string? AdminEmail { get; set; }

        [Phone]
        public string? AdminPhone { get; set; }

        [Required(ErrorMessage = "Password is required."), MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        public string AdminPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm the password.")]
        [DataType(DataType.Password)]
        [Compare("AdminPassword", ErrorMessage = "Passwords do not match.")]
        public string AdminConfirmPassword { get; set; } = string.Empty;

        public IFormFile? AdminProfilePhoto { get; set; }
    }
}
