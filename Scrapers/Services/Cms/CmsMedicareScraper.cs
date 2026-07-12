using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.Cms;

public sealed class CmsMedicareScraper
{
    public static async Task<IReadOnlyList<CmsProviderEntity>> ParseProviderCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        });

        await csv.ReadAsync().ConfigureAwait(false);
        csv.ReadHeader();

        var records = new List<CmsProviderEntity>();
        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            var record = MapRow(csv);
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static CmsProviderEntity? MapRow(CsvReader csv)
    {
        var npi = csv.GetField("NPI") ?? csv.GetField("RNDRNG_NPI");
        if (string.IsNullOrWhiteSpace(npi))
            return null;

        var lastName = csv.GetField("RNDRNG_PRVDNG_LAST_NAME") ?? csv.GetField("Provider Last Name");
        var firstName = csv.GetField("RNDRNG_PRVDNG_FIRST_NAME") ?? csv.GetField("Provider First Name");
        var middleName = csv.GetField("RNDRNG_PRVDNG_MIDDLE_NAME") ?? "";
        var credential = csv.GetField("RNDRNG_PRVDNG_CREDENTIALS") ?? csv.GetField("Provider Credential Text") ?? csv.GetField("Credential");
        var gender = csv.GetField("RNDRNG_PRVDNG_GENDER") ?? csv.GetField("Provider Gender");

        var providerName = string.Join(" ", new[] { firstName, middleName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrWhiteSpace(providerName))
            providerName = $"{lastName}, {firstName}".Trim().TrimStart(',').Trim();

        var medSchool = csv.GetField("MEDICAL_SCHOOL_NAME") ?? csv.GetField("Medical School Name");

        var gradYearStr = csv.GetField("GRADUATION_YEAR") ?? csv.GetField("Graduation Year");
        int? gradYear = int.TryParse(gradYearStr, NumberStyles.None, CultureInfo.InvariantCulture, out var gy) ? gy : null;

        var primarySpecialty = csv.GetField("PRIMARY_SPECIALTY") ?? csv.GetField("Primary Specialty");
        var secondarySpecialty = csv.GetField("SECONDARY_SPECIALTY") ?? csv.GetField("Secondary Specialty");
        var orgLegalName = csv.GetField("ORGANIZATION_LEGAL_NAME") ?? csv.GetField("Organization Legal Name");
        var city = csv.GetField("PRACTICE_CITY") ?? csv.GetField("Practice Address City") ?? csv.GetField("City");
        var state = csv.GetField("PRACTICE_STATE") ?? csv.GetField("Practice Address State") ?? csv.GetField("State");
        var zip = csv.GetField("PRACTICE_ZIP") ?? csv.GetField("Practice Address Zip") ?? csv.GetField("Zip");
        var participation = csv.GetField("MEDICARE_PARTICIPATION") ?? csv.GetField("Medicare Participation");

        var servicesStr = csv.GetField("TOTAL_MEDICARE_SERVICES") ?? csv.GetField("Total Medicare Services");
        int? services = int.TryParse(servicesStr, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null;

        var paymentsStr = csv.GetField("TOTAL_MEDICARE_PAYMENTS") ?? csv.GetField("Total Medicare Payments");
        decimal? payments = decimal.TryParse(paymentsStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;

        var beneficiariesStr = csv.GetField("TOTAL_MEDICARE_BENEFICIARIES") ?? csv.GetField("Total Medicare Beneficiaries");
        int? beneficiaries = int.TryParse(beneficiariesStr, NumberStyles.None, CultureInfo.InvariantCulture, out var b) ? b : null;

        return new CmsProviderEntity
        {
            Npi = npi.Trim(),
            ProviderName = providerName,
            Gender = gender,
            Credential = credential,
            MedicalSchoolName = medSchool,
            GraduationYear = gradYear,
            PrimarySpecialty = primarySpecialty,
            SecondarySpecialty = secondarySpecialty,
            OrganizationLegalName = orgLegalName,
            PracticeAddressCity = city,
            PracticeAddressState = state,
            PracticeAddressZip = zip,
            MedicareParticipation = participation,
            TotalMedicareServices = services,
            TotalMedicarePayments = payments,
            TotalMedicareBeneficiaries = beneficiaries
        };
    }
}
