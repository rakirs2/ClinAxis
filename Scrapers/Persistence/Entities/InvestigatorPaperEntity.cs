using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class InvestigatorPaperEntity
    {
        public Guid InvestigatorPersonId { get; set; }
        public Guid PubmedPaperId { get; set; }
        public int? AuthorPosition { get; set; }
        public bool IsCorrespondingAuthor { get; set; }

        [ForeignKey(nameof(InvestigatorPersonId))]
        public InvestigatorPersonEntity? InvestigatorPerson { get; set; }

        [ForeignKey(nameof(PubmedPaperId))]
        public PubmedPaperEntity? PubmedPaper { get; set; }
    }
}
