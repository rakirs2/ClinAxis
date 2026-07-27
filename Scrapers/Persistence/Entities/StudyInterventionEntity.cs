using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    [Table("study_interventions")]
    public class StudyInterventionEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Study))]
        public string StudyNctId { get; set; } = string.Empty;

        public string? InterventionName { get; set; }
        public string? InterventionType { get; set; }
        public string? Description { get; set; }

        [ForeignKey(nameof(MeshDescriptor))]
        public int? MeshDescriptorId { get; set; }

        public StudyEntity? Study { get; set; }
        public MeshDescriptorEntity? MeshDescriptor { get; set; }
    }
}
