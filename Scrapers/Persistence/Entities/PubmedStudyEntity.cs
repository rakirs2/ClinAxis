using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class PubmedStudyEntity
    {
        [Key]
        public int Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;
        public string Pmid { get; set; } = string.Empty;
        public string? Doi { get; set; }
        public string? Title { get; set; }
        public string? Journal { get; set; }
        public DateTime? PublicationDate { get; set; }
        public string? Abstract { get; set; }
        public bool IsNonEnglish { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }
    }
}