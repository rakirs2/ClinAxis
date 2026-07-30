using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    [Table("study_locations")]
    public class StudyLocationEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Study))]
        public string StudyNctId { get; set; } = string.Empty;

        public string? Facility { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }

        public int? MeshDescriptorId { get; set; }

        [ForeignKey(nameof(MeshDescriptorId))]
        public MeshDescriptorEntity? MeshDescriptor { get; set; }

        public StudyEntity? Study { get; set; }
    }
}
