using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class StudyInvestigatorEntity
    {
        [Key]
        public int Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;
        public Guid InvestigatorPersonId { get; set; }
        public string? RoleOnStudy { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public bool IsOverallOfficial { get; set; }

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }

        [ForeignKey(nameof(InvestigatorPersonId))]
        public InvestigatorPersonEntity? InvestigatorPerson { get; set; }
    }
}
