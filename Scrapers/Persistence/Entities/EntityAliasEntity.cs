using System;
using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class EntityAliasEntity
    {
        [Key]
        public long Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string CanonicalId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceEntityId { get; set; } = string.Empty;
        public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
