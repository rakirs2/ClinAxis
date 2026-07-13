using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    [Table("study_outcomes")]
    public class StudyOutcomeEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Study))]
        public string StudyNctId { get; set; } = string.Empty;

        public string OutcomeType { get; set; } = string.Empty;
        public string? Measure { get; set; }
        public string? Description { get; set; }
        public string? TimeFrame { get; set; }

        public StudyEntity? Study { get; set; }
    }
}
