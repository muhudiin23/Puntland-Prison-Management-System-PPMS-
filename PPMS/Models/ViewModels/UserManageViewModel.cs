using System.ComponentModel.DataAnnotations;

namespace PPMS.Models.ViewModels
{
    public class UserManageViewModel
    {
        public string? Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string Role { get; set; } = "StaffUser";

        public int? AssignedPrisonId { get; set; }

        public bool IsActive { get; set; } = true;

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password), Compare("Password")]
        public string? ConfirmPassword { get; set; }
    }
}
