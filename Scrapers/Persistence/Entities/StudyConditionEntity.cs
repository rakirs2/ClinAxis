using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class StudyConditionEntity
    {
        [Key]
        public int Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }
    }
}