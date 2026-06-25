using System.ComponentModel.DataAnnotations;

namespace PPMS.Models
{
    public class WantedCriminal
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string CriminalName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string NationalId { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string CrimeDescription { get; set; } = string.Empty;

        [MaxLength(300)]
        public string LastKnownLocation { get; set; } = string.Empty;

        [Required]
        public string RiskLevel { get; set; } = "Medium";

        public string? PhotoPath { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
