using System.ComponentModel.DataAnnotations;

namespace PPMS.Models
{
    public class PrisonerTransfer
    {
        public int Id { get; set; }

        public int PrisonerId { get; set; }
        public Prisoner Prisoner { get; set; } = null!;

        public int SourcePrisonId { get; set; }
        public Prison SourcePrison { get; set; } = null!;

        public int DestinationPrisonId { get; set; }
        public Prison DestinationPrison { get; set; } = null!;

        [Required, MaxLength(500)]
        public string TransferReason { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? TransferNotes { get; set; }

        public string? AuthorizedBy { get; set; }

        // Pending | Approved | Completed | Rejected
        public string Status { get; set; } = "Pending";

        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public string? RejectionReason { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
