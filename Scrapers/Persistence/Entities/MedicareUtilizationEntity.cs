using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

public class MedicareUtilizationEntity
{
    [Key]
    public int Id { get; set; }

    public Guid InvestigatorPersonId { get; set; }

    public int DataYear { get; set; }

    public string? ProviderType { get; set; }

    public int? TotalBeneficiaries { get; set; }

    public long? TotalServices { get; set; }

    public decimal? TotalSubmittedCharges { get; set; }

    public decimal? TotalMedicareAllowedAmount { get; set; }

    public decimal? TotalMedicarePaymentAmount { get; set; }

    public decimal? TotalMedicareStandardizedAmount { get; set; }

    public string? MedicareParticipationIndicator { get; set; }

    public int? BeneAgeLt65Count { get; set; }

    public int? BeneAge65To74Count { get; set; }

    public int? BeneAge75To84Count { get; set; }

    public int? BeneAgeGt84Count { get; set; }

    public int? BeneFemaleCount { get; set; }

    public int? BeneMaleCount { get; set; }

    public int? BeneDualCount { get; set; }

    public int? BeneNonDualCount { get; set; }

    public string? ChronicConditionsJson { get; set; }

    public decimal? AvgRiskScore { get; set; }

    public long? MedicalServices { get; set; }

    public long? DrugServices { get; set; }

    public decimal? MedicalMedicarePayment { get; set; }

    public decimal? DrugMedicarePayment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public InvestigatorPersonEntity InvestigatorPerson { get; set; } = null!;
}
