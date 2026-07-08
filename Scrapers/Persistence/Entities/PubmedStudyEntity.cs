using System;

namespace Scrapers.Persistence.Entities
{
    public class PubmedStudyEntity
    {
        public int Id { get; set; }
        public int InvestigatorId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? Keywords { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public InvestigatorEntity? Investigator { get; set; }
    }
}
