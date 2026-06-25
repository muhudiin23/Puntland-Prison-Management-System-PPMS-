using System.ComponentModel.DataAnnotations;

namespace PPMS.Models
{
    public class Alert
    {
        public int Id { get; set; }

        [Required]
        public string AlertType { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        public string Severity { get; set; } = "Medium";

        public bool IsActive { get; set; } = true;

        public bool IsDismissed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DismissedAt { get; set; }

        public string? CreatedBy { get; set; }

        public int? PrisonId { get; set; }
    }
}
