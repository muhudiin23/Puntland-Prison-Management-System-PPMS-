using System.ComponentModel.DataAnnotations;

namespace PPMS.Models
{
    public class Activity
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public string ActivityType { get; set; } = string.Empty;

        public string? UserId { get; set; }

        public string? UserName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? PrisonId { get; set; }
    }
}
