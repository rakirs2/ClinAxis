using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    [Table("mesh_tree_paths")]
    public class MeshTreePathEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(MeshDescriptor))]
        public int MeshDescriptorId { get; set; }

        public string TreeNumber { get; set; } = string.Empty;

        public MeshDescriptorEntity? MeshDescriptor { get; set; }
    }
}
