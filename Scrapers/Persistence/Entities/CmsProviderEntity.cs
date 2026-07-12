using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

public class CmsProviderEntity
{
    [Key]
    public int Id { get; set; }

    public Guid Uuid { get; set; }

    public string Npi { get; set; } = string.Empty;

    public string? ProviderName { get; set; }
    public string? Gender { get; set; }
    public string? Credential { get; set; }
    public string? MedicalSchoolName { get; set; }
    public int? GraduationYear { get; set; }
    public string? PrimarySpecialty { get; set; }
    public string? SecondarySpecialty { get; set; }
    public string? OrganizationLegalName { get; set; }
    public string? PracticeAddressCity { get; set; }
    public string? PracticeAddressState { get; set; }
    public string? PracticeAddressZip { get; set; }
    public string? MedicareParticipation { get; set; }
    public int? TotalMedicareServices { get; set; }
    public decimal? TotalMedicarePayments { get; set; }
    public int? TotalMedicareBeneficiaries { get; set; }
}
