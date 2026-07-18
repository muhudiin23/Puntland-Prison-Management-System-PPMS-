using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMS.Models
{
    public class Staff
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string StaffIdNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(150), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Gender { get; set; } = "Male";

        [Required, MaxLength(100)]
        public string Role { get; set; } = string.Empty;

        public int PrisonId { get; set; }

        [ForeignKey("PrisonId")]
        public Prison? Prison { get; set; }

        [MaxLength(100)]
        public string ShiftSchedule { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? PhotoPath { get; set; }

        public ICollection<StaffCertificate> Certificates { get; set; } = new List<StaffCertificate>();
    }
}
