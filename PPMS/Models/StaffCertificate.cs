using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMS.Models
{
    public class StaffCertificate
    {
        public int Id { get; set; }

        public int StaffId { get; set; }

        [ForeignKey("StaffId")]
        public Staff? Staff { get; set; }

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required, MaxLength(300)]
        public string OriginalName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string FileType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [MaxLength(256)]
        public string? UploadedBy { get; set; }
    }
}
