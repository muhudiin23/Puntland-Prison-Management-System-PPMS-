using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMS.Models
{
    public class Prisoner
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string PrisonerId { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        public string Gender { get; set; } = "Male";

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required, MaxLength(200)]
        public string CrimeType { get; set; } = string.Empty;

        [Required]
        public int SentenceDurationMonths { get; set; }

        [Required]
        public DateTime EntryDate { get; set; }

        public DateTime ReleaseDate { get; set; }

        [Required]
        public string CriminalStatus { get; set; } = "Active";

        [MaxLength(300)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(150)]
        public string EmergencyContact { get; set; } = string.Empty;

        public string? PhotoPath { get; set; }

        public string? FingerprintData { get; set; }

        public int PrisonId { get; set; }

        [ForeignKey("PrisonId")]
        public Prison? Prison { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ── Soft-delete / archive ──────────────────────────────────
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }

        [MaxLength(50)]
        public string? ArchiveReason { get; set; }   // Released | Transferred | Deceased | Administrative

        [MaxLength(256)]
        public string? ArchivedBy { get; set; }

        [MaxLength(500)]
        public string? ArchiveNotes { get; set; }

        public ICollection<PrisonerEvidence> Evidences { get; set; } = new List<PrisonerEvidence>();
    }
}
