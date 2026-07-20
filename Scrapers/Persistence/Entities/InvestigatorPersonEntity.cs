using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities
{
    public class InvestigatorPersonEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Orcid { get; set; }
        public string? NcbiId { get; set; }
        public string? Npi { get; set; }
        public DateTime? NpiLookupAttemptedAt { get; set; }
        public string? NpiEnrichmentResult { get; set; }
        public DateTime? MedicareLookupAttemptedAt { get; set; }
        public string? MedicareLookupResult { get; set; }
        public string Source { get; set; } = "ClinicalTrials.gov";
        public bool IsHuman { get; set; } = true;
        public DateTime? VerifiedAt { get; set; }
        public string? VerificationSource { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<StudyInvestigatorEntity>? StudyInvestigators { get; set; }
        public ICollection<InvestigatorAffiliationEntity>? Affiliations { get; set; }
        public ICollection<InvestigatorPaperEntity>? InvestigatorPapers { get; set; }
        public ICollection<PersonIdentifierCandidateEntity>? IdentifierCandidates { get; set; }
        public ICollection<MedicareUtilizationEntity>? MedicareUtilizations { get; set; }
        public ICollection<InvestigatorMetricEntity>? Metrics { get; set; }
    }
}
