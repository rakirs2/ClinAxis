using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class PiAggregationEntity
    {
        [Key]
        public int Id { get; set; }
        public string InvestigatorName { get; set; } = string.Empty;
        public string? Affiliation { get; set; }
        public int StudyCount { get; set; }
        public int PubmedPaperCount { get; set; }
        public int PubmedTrialCount { get; set; }
        public int PubmedReviewCount { get; set; }
        public int PubmedOtherCount { get; set; }
        public string StudyNctIds { get; set; } = string.Empty;
        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}