using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class RejectedEntityEntity
    {
        [Key]
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string StudyNctId { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? Affiliation { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime RejectedAt { get; set; } = DateTime.UtcNow;
    }
}
