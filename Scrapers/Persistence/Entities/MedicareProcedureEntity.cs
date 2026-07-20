using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

public class MedicareProcedureEntity
{
    [Key]
    public int Id { get; set; }

    public Guid InvestigatorPersonId { get; set; }

    public int DataYear { get; set; }

    public string HcpcsCode { get; set; } = string.Empty;

    public string? HcpcsDescription { get; set; }

    public string? PlaceOfService { get; set; }

    public int? BeneficiaryCount { get; set; }

    public long? ServiceCount { get; set; }

    public decimal? SubmittedChargeAmount { get; set; }

    public decimal? MedicareAllowedAmount { get; set; }

    public decimal? MedicarePaymentAmount { get; set; }

    public string? ProviderType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public InvestigatorPersonEntity InvestigatorPerson { get; set; } = null!;
}
