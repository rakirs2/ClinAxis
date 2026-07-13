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
        public DateTime RejectedAt { get; set; } = DateTime.UtcNow;
    }
}
