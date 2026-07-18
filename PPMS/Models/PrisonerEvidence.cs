using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMS.Models
{
    public class PrisonerEvidence
    {
        public int Id { get; set; }

        public int PrisonerId { get; set; }

        [ForeignKey("PrisonerId")]
        public Prisoner? Prisoner { get; set; }

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required, MaxLength(300)]
        public string OriginalName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string FileType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(100)]
        public string DocumentType { get; set; } = "Other";

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [MaxLength(256)]
        public string? UploadedBy { get; set; }
    }
}
