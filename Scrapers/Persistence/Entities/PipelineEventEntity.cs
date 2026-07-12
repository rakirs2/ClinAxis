using System;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class PipelineEventEntity
    {
        [Key]
        public long Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PickedUpAt { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
    }
}
