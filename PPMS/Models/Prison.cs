using System.ComponentModel.DataAnnotations;

namespace PPMS.Models
{
    public class Prison
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string PrisonName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        public int Capacity { get; set; }

        public int CurrentPopulation { get; set; }

        [Required]
        public string SecurityLevel { get; set; } = "Medium";

        [MaxLength(300)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(20)]
        public string ContactNumber { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Prisoner> Prisoners { get; set; } = new List<Prisoner>();
        public ICollection<Staff> Staff { get; set; } = new List<Staff>();
    }
}
