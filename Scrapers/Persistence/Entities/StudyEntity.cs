using System;

namespace Scrapers.Persistence.Entities
{
    public class StudyEntity
    {
        public int Id { get; set; }
        public string NctId { get; set; } = string.Empty;
        public string? BriefTitle { get; set; }
        public string? OverallStatus { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsIncomplete { get; set; } = false;

        public System.Collections.Generic.ICollection<InvestigatorEntity>? Investigators { get; set; }
    }
}
