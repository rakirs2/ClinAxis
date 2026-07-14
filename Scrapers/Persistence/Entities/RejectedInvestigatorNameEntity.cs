using System;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class RejectedInvestigatorNameEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string FullName { get; set; } = string.Empty;

        public int OccurrenceCount { get; set; }

        public int StudyCount { get; set; }

        public string? RejectionReason { get; set; }

        public bool? IsHumanOverride { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
