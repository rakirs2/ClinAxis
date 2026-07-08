using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class InvestigatorEntity
    {
        [Key]
        public int Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Affiliation { get; set; }

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }
    }
}
