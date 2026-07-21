using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class OpenPaymentsIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task StoreOpenPayment_CreatesEntity_WithAllFields()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. John Smith",
            IsHuman = true,
            Npi = "1234567890",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var payment = new OpenPaymentEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            PaymentType = "research",
            PaymentAmount = 15000.00m,
            PaymentDate = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            PayorName = "PharmaCorp Inc.",
            NatureOfPayment = "Research",
            FormOfPayment = "Cash or cash equivalent",
            StudyName = "Phase 3 Clinical Trial for Drug X",
            ClinicalTrialsId = "NCT01234567",
            ContextOfResearch = "Clinical Trial",
            ProductCategory = "Oncology",
            ProductName = "Drug X",
            RecordId = "1000001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.OpenPayments.Add(payment);
        await Context.SaveChangesAsync();

        var saved = await Context.OpenPayments
            .FirstOrDefaultAsync(o => o.InvestigatorPersonId == person.Id);

        Assert.IsNotNull(saved);
        Assert.AreEqual(2024, saved.DataYear);
        Assert.AreEqual("research", saved.PaymentType);
        Assert.AreEqual(15000.00m, saved.PaymentAmount);
        Assert.AreEqual(new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc), saved.PaymentDate);
        Assert.AreEqual("PharmaCorp Inc.", saved.PayorName);
        Assert.AreEqual("Research", saved.NatureOfPayment);
        Assert.AreEqual("Cash or cash equivalent", saved.FormOfPayment);
        Assert.AreEqual("Phase 3 Clinical Trial for Drug X", saved.StudyName);
        Assert.AreEqual("NCT01234567", saved.ClinicalTrialsId);
        Assert.AreEqual("Clinical Trial", saved.ContextOfResearch);
        Assert.AreEqual("Oncology", saved.ProductCategory);
        Assert.AreEqual("Drug X", saved.ProductName);
        Assert.AreEqual("1000001", saved.RecordId);
        Assert.IsNotNull(saved.CreatedAt);
        Assert.IsNotNull(saved.UpdatedAt);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task MultiplePayments_ForSamePerson_AreRetrievable()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Multi Payment",
            IsHuman = true,
            Npi = "0987654321",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var paymentTypes = new[] { "research", "general", "ownership" };
        foreach (var type in paymentTypes)
        {
            Context.OpenPayments.Add(new OpenPaymentEntity
            {
                InvestigatorPersonId = person.Id,
                DataYear = 2024,
                PaymentType = type,
                PaymentAmount = 1000m,
                RecordId = type,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await Context.SaveChangesAsync();

        var records = await Context.OpenPayments
            .Where(o => o.InvestigatorPersonId == person.Id)
            .OrderBy(o => o.PaymentType)
            .ToListAsync();

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual("general", records[0].PaymentType);
        Assert.AreEqual("ownership", records[1].PaymentType);
        Assert.AreEqual("research", records[2].PaymentType);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Payments_AccessibleViaNavigationProperty()
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
        Context.OpenPayments.Add(new OpenPaymentEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            PaymentType = "research",
            PaymentAmount = 5000m,
            RecordId = "nav001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        var personWithNav = await Context.InvestigatorPersons
            .Include(p => p.OpenPayments)
            .FirstOrDefaultAsync(p => p.Id == person.Id);

        Assert.IsNotNull(personWithNav);
        Assert.IsNotNull(personWithNav.OpenPayments);
        Assert.AreEqual(1, personWithNav.OpenPayments.Count);
        Assert.AreEqual("research", personWithNav.OpenPayments.First().PaymentType);
        Assert.AreEqual(5000m, personWithNav.OpenPayments.First().PaymentAmount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DuplicateRecordId_ThrowsUniqueConstraintViolation()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Duplicate",
            IsHuman = true,
            Npi = "5566778899",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        Context.OpenPayments.Add(new OpenPaymentEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            PaymentType = "research",
            PaymentAmount = 1000m,
            RecordId = "dup001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Context.OpenPayments.Add(new OpenPaymentEntity
        {
            InvestigatorPersonId = person.Id,
            DataYear = 2024,
            PaymentType = "research",
            PaymentAmount = 2000m,
            RecordId = "dup001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsExceptionAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }
}
