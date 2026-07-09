using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class CategoryAggregationEntity
    {
        [Key]
        public int Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryType { get; set; } = string.Empty;
        public int StudyCount { get; set; }
        public int PubmedPaperCount { get; set; }
        public string StudyNctIds { get; set; } = string.Empty;
        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}