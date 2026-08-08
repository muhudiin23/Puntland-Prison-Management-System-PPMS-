using System.ComponentModel.DataAnnotations;

namespace PPMS.Models
{
    public class FormerPrisoner
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
        public string CriminalStatus { get; set; } = "Released";

        [MaxLength(300)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(150)]
        public string EmergencyContact { get; set; } = string.Empty;

        public string? PhotoPath { get; set; }
        public string? FingerprintData { get; set; }

        // Prison info stored as strings — no FK to avoid cascade/orphan issues
        public int? OriginalPrisonId { get; set; }

        [Required, MaxLength(200)]
        public string PrisonName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? PrisonCity { get; set; }

        // Original record timestamps
        public DateTime OriginalCreatedAt { get; set; }
        public DateTime OriginalUpdatedAt { get; set; }

        // Archive metadata
        public DateTime? ArchivedAt { get; set; }

        [MaxLength(50)]
        public string? ArchiveReason { get; set; }

        [MaxLength(256)]
        public string? ArchivedBy { get; set; }

        [MaxLength(500)]
        public string? ArchiveNotes { get; set; }

        // Number of evidence files that existed at archive time (physical files stay on disk)
        public int EvidenceCount { get; set; }

        public DateTime RecordMovedAt { get; set; } = DateTime.Now;
    }
}
