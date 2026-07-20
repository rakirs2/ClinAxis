using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class PubmedPaperEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Pmid { get; set; } = string.Empty;
        public string? Doi { get; set; }
        public string? Title { get; set; }
        public string? Journal { get; set; }
        public DateTime? PublicationDate { get; set; }
        public string? Abstract { get; set; }
        public bool IsNonEnglish { get; set; }
        public string? PublicationTypes { get; set; }
        public string Source { get; set; } = "PubMed/EUtils";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<StudyPaperEntity>? StudyPapers { get; set; }
        public ICollection<InvestigatorPaperEntity>? InvestigatorPapers { get; set; }
    }
}
