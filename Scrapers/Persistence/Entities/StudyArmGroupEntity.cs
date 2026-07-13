using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    [Table("study_arm_groups")]
    public class StudyArmGroupEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Study))]
        public string StudyNctId { get; set; } = string.Empty;

        public string? Label { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }

        public StudyEntity? Study { get; set; }
    }
}
