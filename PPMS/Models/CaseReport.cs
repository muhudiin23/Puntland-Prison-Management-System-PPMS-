using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMS.Models
{
    public class CaseReport
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string CaseId { get; set; } = string.Empty;       // CASE-2026-0001

        [Required, MaxLength(200)]
        public string CaseTitle { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string CrimeType { get; set; } = string.Empty;

        [Required]
        public string CrimeDescription { get; set; } = string.Empty;

        [MaxLength(150)]
        public string PrisonerName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string NationalId { get; set; } = string.Empty;

        public int? PrisonId { get; set; }

        [ForeignKey("PrisonId")]
        public Prison? Prison { get; set; }

        [Required]
        public DateTime DateOfCrime { get; set; }

        [Required]
        public DateTime DateReported { get; set; }

        [Required, MaxLength(50)]
        public string InvestigationStatus { get; set; } = "Pending";
        // Pending | Ongoing | Completed | Suspended

        [Required, MaxLength(50)]
        public string CaseStatus { get; set; } = "Open";
        // Open | Under Review | Closed | Dismissed

        [MaxLength(150)]
        public string OfficerInCharge { get; set; } = string.Empty;

        public string? DocumentPath { get; set; }
        public string? EvidencePath { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(256)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
