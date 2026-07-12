using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class StudyAuthorEntity
    {
        [Key]
        public int Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;
        public string Pmid { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? ForeName { get; set; }
        public string? Orcid { get; set; }
        public string? NcbiId { get; set; }
        public Guid? InvestigatorUuid { get; set; }

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }
    }
}
