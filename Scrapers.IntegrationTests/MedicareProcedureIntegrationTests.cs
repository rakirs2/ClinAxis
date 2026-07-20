using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class MedicareProcedureIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task StoreMedicareProcedure_CreatesEntity_WithAllFields()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Procedure Test",
            IsHuman = true,
            Npi = "1234567890",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var procedure = new MedicareProcedureEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            HcpcsCode = "99214",
            HcpcsDescription = "Office/outpatient visit est low to high 30-39 min",
            PlaceOfService = "O",
            BeneficiaryCount = 85,
            ServiceCount = 180,
            SubmittedChargeAmount = 36000.00m,
            MedicareAllowedAmount = 25200.00m,
            MedicarePaymentAmount = 19000.00m,
            ProviderType = "Internal Medicine",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.MedicareProcedures.Add(procedure);
        await Context.SaveChangesAsync();

        var saved = await Context.MedicareProcedures
            .FirstOrDefaultAsync(p => p.InvestigatorPersonId == person.Id);

        Assert.IsNotNull(saved);
        Assert.AreEqual("99214", saved.HcpcsCode);
        Assert.AreEqual("Office/outpatient visit est low to high 30-39 min", saved.HcpcsDescription);
        Assert.AreEqual("O", saved.PlaceOfService);
        Assert.AreEqual(85, saved.BeneficiaryCount);
        Assert.AreEqual(180, saved.ServiceCount);
        Assert.AreEqual(36000.00m, saved.SubmittedChargeAmount);
        Assert.AreEqual(25200.00m, saved.MedicareAllowedAmount);
        Assert.AreEqual(19000.00m, saved.MedicarePaymentAmount);
        Assert.AreEqual("Internal Medicine", saved.ProviderType);
        Assert.AreEqual(2024, saved.DataYear);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task MultipleProcedures_ForSamePerson_AreRetrievable()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Multi Procedure",
            IsHuman = true,
            Npi = "0987654321",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var codes = new[] { "99214", "99396", "93000" };
        foreach (var code in codes)
        {
            Context.MedicareProcedures.Add(new MedicareProcedureEntity
            {
                InvestigatorPersonId = person.Id,
                DataYear = 2024,
                HcpcsCode = code,
                ServiceCount = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await Context.SaveChangesAsync();

        var records = await Context.MedicareProcedures
            .Where(p => p.InvestigatorPersonId == person.Id)
            .OrderBy(p => p.HcpcsCode)
            .ToListAsync();

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual("93000", records[0].HcpcsCode);
        Assert.AreEqual("99214", records[1].HcpcsCode);
        Assert.AreEqual("99396", records[2].HcpcsCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ProcedureData_AccessibleViaNavigationProperty()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Nav Procedure",
            IsHuman = true,
            Npi = "1122334455",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        Context.MedicareProcedures.Add(new MedicareProcedureEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            HcpcsCode = "99214",
            ServiceCount = 180,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        var personWithNav = await Context.InvestigatorPersons
            .Include(p => p.Procedures)
            .FirstOrDefaultAsync(p => p.Id == person.Id);

        Assert.IsNotNull(personWithNav);
        Assert.IsNotNull(personWithNav.Procedures);
        Assert.AreEqual(1, personWithNav.Procedures.Count);
        Assert.AreEqual("99214", personWithNav.Procedures.First().HcpcsCode);
        Assert.AreEqual(180, personWithNav.Procedures.First().ServiceCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UniqueIndex_OnSamePersonYearCodePlace_Throws()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Unique Procedure",
            IsHuman = true,
            Npi = "5566778899",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        Context.MedicareProcedures.Add(new MedicareProcedureEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            HcpcsCode = "99214",
            PlaceOfService = "O",
            ServiceCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.MedicareProcedures.Add(new MedicareProcedureEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            HcpcsCode = "99214",
            PlaceOfService = "O",
            ServiceCount = 200,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsExceptionAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }
}
