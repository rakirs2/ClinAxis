using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class MedicareUtilizationIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task StoreMedicareUtilization_CreatesEntity_WithAllFields()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Test Physician",
            IsHuman = true,
            Npi = "1234567890",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var util = new MedicareUtilizationEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            ProviderType = "Internal Medicine",
            TotalBeneficiaries = 150,
            TotalServices = 450,
            TotalSubmittedCharges = 125000.00m,
            TotalMedicareAllowedAmount = 87500.00m,
            TotalMedicarePaymentAmount = 65000.00m,
            TotalMedicareStandardizedAmount = 72000.00m,
            MedicareParticipationIndicator = "Y",
            BeneAgeLt65Count = 10,
            BeneAge65To74Count = 80,
            BeneAge75To84Count = 45,
            BeneAgeGt84Count = 15,
            BeneFemaleCount = 85,
            BeneMaleCount = 65,
            BeneDualCount = 30,
            BeneNonDualCount = 120,
            AvgRiskScore = 2.5m,
            MedicalServices = 380,
            DrugServices = 70,
            MedicalMedicarePayment = 52000.00m,
            DrugMedicarePayment = 13000.00m,
            ChronicConditionsJson = JsonSerializer.Serialize(new Dictionary<string, decimal?>
            {
                ["Hypertension"] = 42.5m,
                ["Diabetes"] = 22.1m
            }),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.MedicareUtilizations.Add(util);
        await Context.SaveChangesAsync();

        var saved = await Context.MedicareUtilizations
            .FirstOrDefaultAsync(m => m.InvestigatorPersonId == person.Id);

        Assert.IsNotNull(saved);
        Assert.AreEqual(2024, saved.DataYear);
        Assert.AreEqual("Internal Medicine", saved.ProviderType);
        Assert.AreEqual(150, saved.TotalBeneficiaries);
        Assert.AreEqual(450, saved.TotalServices);
        Assert.AreEqual(125000.00m, saved.TotalSubmittedCharges);
        Assert.AreEqual(87500.00m, saved.TotalMedicareAllowedAmount);
        Assert.AreEqual(65000.00m, saved.TotalMedicarePaymentAmount);
        Assert.AreEqual(72000.00m, saved.TotalMedicareStandardizedAmount);
        Assert.AreEqual("Y", saved.MedicareParticipationIndicator);
        Assert.AreEqual(10, saved.BeneAgeLt65Count);
        Assert.AreEqual(80, saved.BeneAge65To74Count);
        Assert.AreEqual(45, saved.BeneAge75To84Count);
        Assert.AreEqual(15, saved.BeneAgeGt84Count);
        Assert.AreEqual(85, saved.BeneFemaleCount);
        Assert.AreEqual(65, saved.BeneMaleCount);
        Assert.AreEqual(30, saved.BeneDualCount);
        Assert.AreEqual(120, saved.BeneNonDualCount);
        Assert.AreEqual(2.5m, saved.AvgRiskScore);
        Assert.AreEqual(380, saved.MedicalServices);
        Assert.AreEqual(70, saved.DrugServices);
        Assert.AreEqual(52000.00m, saved.MedicalMedicarePayment);
        Assert.AreEqual(13000.00m, saved.DrugMedicarePayment);
        Assert.IsNotNull(saved.ChronicConditionsJson);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task MultipleUtilizations_ForSamePerson_AreRetrievable()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Multi Year",
            IsHuman = true,
            Npi = "0987654321",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        for (int year = 2022; year <= 2024; year++)
        {
            Context.MedicareUtilizations.Add(new MedicareUtilizationEntity
            {
                InvestigatorPersonId = person.Id,
                DataYear = year,
                TotalBeneficiaries = 100 + year,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await Context.SaveChangesAsync();

        var records = await Context.MedicareUtilizations
            .Where(m => m.InvestigatorPersonId == person.Id)
            .OrderByDescending(m => m.DataYear)
            .ToListAsync();

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual(2024, records[0].DataYear);
        Assert.AreEqual(2023, records[1].DataYear);
        Assert.AreEqual(2022, records[2].DataYear);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UtilizationData_AccessibleViaNavigationProperty()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Navigation Test",
            IsHuman = true,
            Npi = "1122334455",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        Context.MedicareUtilizations.Add(new MedicareUtilizationEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            TotalBeneficiaries = 200,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        var personWithNav = await Context.InvestigatorPersons
            .Include(p => p.MedicareUtilizations)
            .FirstOrDefaultAsync(p => p.Id == person.Id);

        Assert.IsNotNull(personWithNav);
        Assert.IsNotNull(personWithNav.MedicareUtilizations);
        Assert.AreEqual(1, personWithNav.MedicareUtilizations.Count);
        Assert.AreEqual(2024, personWithNav.MedicareUtilizations.First().DataYear);
        Assert.AreEqual(200, personWithNav.MedicareUtilizations.First().TotalBeneficiaries);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task MedicareLookupFields_AreUpdatedOnPerson()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Lookup Test",
            IsHuman = true,
            Npi = "5566778899",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        person.MedicareLookupAttemptedAt = DateTime.UtcNow;
        person.MedicareLookupResult = "found";
        person.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        var saved = await Context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.Id == person.Id);

        Assert.IsNotNull(saved);
        Assert.IsNotNull(saved.MedicareLookupAttemptedAt);
        Assert.AreEqual("found", saved.MedicareLookupResult);
    }
}
