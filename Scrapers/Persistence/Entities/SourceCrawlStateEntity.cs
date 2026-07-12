using System;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class SourceCrawlStateEntity
    {
        [Key]
        public int Id { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string? LastCursor { get; set; }
        public DateTime? LastStartedAt { get; set; }
        public DateTime? LastSuccessAt { get; set; }
        public int TotalRecordsFetched { get; set; }
        public string Status { get; set; } = "Idle";
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
