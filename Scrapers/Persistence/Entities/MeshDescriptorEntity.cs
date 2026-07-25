using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class MeshDescriptorEntity
    {
        [Key]
        public int Id { get; set; }
        public string Cui { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ICollection<string> TreeNumbers { get; set; } = [];
        public string Category { get; set; } = string.Empty;

        public ICollection<StudyConditionEntity>? StudyConditions { get; set; }
    }
}
