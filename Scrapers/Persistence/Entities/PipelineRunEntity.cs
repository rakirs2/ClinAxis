using System;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class PipelineRunEntity
    {
        [Key]
        public int Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "Running";
        public int? TotalStudies { get; set; }
        public int? TotalInvestigators { get; set; }
        public int? TotalPubmedPapers { get; set; }
        public int? TotalKeywords { get; set; }
        public int? TotalAuthors { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
