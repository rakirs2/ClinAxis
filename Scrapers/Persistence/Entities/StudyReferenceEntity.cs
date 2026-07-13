using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    [Table("study_references")]
    public class StudyReferenceEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Study))]
        public string StudyNctId { get; set; } = string.Empty;

        public string? Pmid { get; set; }
        public string? Citation { get; set; }
        public string? Type { get; set; }

        public StudyEntity? Study { get; set; }
    }
}
