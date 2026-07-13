using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class StudyPaperEntity
    {
        public string StudyNctId { get; set; } = string.Empty;
        public Guid PubmedPaperId { get; set; }

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }

        [ForeignKey(nameof(PubmedPaperId))]
        public PubmedPaperEntity? PubmedPaper { get; set; }
    }
}
