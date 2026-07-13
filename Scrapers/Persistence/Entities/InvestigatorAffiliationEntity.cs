using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class InvestigatorAffiliationEntity
    {
        [Key]
        public int Id { get; set; }
        public Guid InvestigatorPersonId { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Role { get; set; }
        public bool IsPrimary { get; set; }

        [ForeignKey(nameof(InvestigatorPersonId))]
        public InvestigatorPersonEntity? InvestigatorPerson { get; set; }
    }
}
