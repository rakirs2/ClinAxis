using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class StudyConditionEntity
    {
        [Key]
        public int Id { get; set; }
        public string StudyNctId { get; set; } = string.Empty;

        public int MeshDescriptorId { get; set; }

        [ForeignKey(nameof(MeshDescriptorId))]
        public MeshDescriptorEntity? MeshDescriptor { get; set; }

        [ForeignKey(nameof(StudyNctId))]
        public StudyEntity? Study { get; set; }
    }
}