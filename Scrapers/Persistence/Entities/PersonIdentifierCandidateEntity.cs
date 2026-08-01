using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class PersonIdentifierCandidateEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PersonId { get; set; }
        public string IdentifierType { get; set; } = string.Empty;
        public string IdentifierValue { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string? MatchedFullName { get; set; }
        public string? MatchedAffiliation { get; set; }
        public string? MatchedState { get; set; }
        public string? MatchedCity { get; set; }
        public string? MatchedMiddleName { get; set; }
        public string? MatchedCredential { get; set; }
        public string? MatchedNamePrefix { get; set; }
        public string? MatchedGender { get; set; }
        public string? MatchedTaxonomyDesc { get; set; }
        public string? MatchedTaxonomyState { get; set; }
        public string? MatchedTaxonomyLicense { get; set; }
        public string? MatchedOtherNamesJson { get; set; }
        public string? MatchedIdentifiersJson { get; set; }
        public double? RuleScore { get; set; }
        public string? SourceStatus { get; set; }
        public DateTime? SourceDeactivatedAt { get; set; }
        public bool IsAutoApproved { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public InvestigatorPersonEntity Person { get; set; } = null!;
    }
}
