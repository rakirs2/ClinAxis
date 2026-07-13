using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class StudyEntity
    {
        [Key]
        public string NctId { get; set; } = string.Empty;
        public string? BriefTitle { get; set; }
        public string? OfficialTitle { get; set; }
        public string? OverallStatus { get; set; }
        public string? StudyType { get; set; }
        public string? BriefSummary { get; set; }
        public string? PrimaryPurpose { get; set; }
        public string? InterventionModel { get; set; }
        public string? Allocation { get; set; }
        public int? EnrollmentCount { get; set; }
        public string? Sex { get; set; }
        public string? MinimumAge { get; set; }
        public string? MaximumAge { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? CompletionDate { get; set; }
        public DateOnly? StudyFirstPostDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsIncomplete { get; set; }

        public ICollection<InvestigatorEntity>? Investigators { get; set; }
        public ICollection<StudyInvestigatorEntity>? StudyInvestigators { get; set; }
        public ICollection<StudyPaperEntity>? StudyPapers { get; set; }
        public ICollection<StudyKeywordEntity>? Keywords { get; set; }
        public ICollection<StudyConditionEntity>? Conditions { get; set; }
        public ICollection<StudyPhaseEntity>? Phases { get; set; }
        public ICollection<StudyLocationEntity>? Locations { get; set; }
        public ICollection<StudyReferenceEntity>? References { get; set; }
    }
}
