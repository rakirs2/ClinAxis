using System;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class StudyReferenceEntity
    {
        [Key]
        public long Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;
        public string? Pmid { get; set; }
        public string? Doi { get; set; }
        public string? Citation { get; set; }
        public string? Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
