using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class InvestigatorMetricsIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateMetric_StoresHIndexAndCitations()
    {
        // Arrange
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Test Researcher",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var metric = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            HIndex = 25,
            CitationCount = 2500,
            I10Index = 45,
            TotalPapers = 89,
            ExternalAuthorId = "1234567890",
            LookupResult = "found",
            LookupAttemptedAt = DateTime.UtcNow
        };
        Context.InvestigatorMetrics.Add(metric);
        await Context.SaveChangesAsync();

        // Act
        var saved = await Context.InvestigatorMetrics.FindAsync(metric.Id);

        // Assert
        Assert.IsNotNull(saved);
        Assert.AreEqual("SemanticScholar", saved.Source);
        Assert.AreEqual(25, saved.HIndex);
        Assert.AreEqual(2500, saved.CitationCount);
        Assert.AreEqual(45, saved.I10Index);
        Assert.AreEqual(89, saved.TotalPapers);
        Assert.AreEqual("1234567890", saved.ExternalAuthorId);
        Assert.AreEqual("found", saved.LookupResult);
        Assert.IsNotNull(saved.LookupAttemptedAt);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task MultipleMetrics_PerInvestigator_StoredIndependently()
    {
        // Arrange
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Multi Source",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var semanticMetric = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            HIndex = 15,
            CitationCount = 1000
        };
        Context.InvestigatorMetrics.Add(semanticMetric);
        await Context.SaveChangesAsync();

        // Act
        var metrics = Context.InvestigatorMetrics.Where(m => m.InvestigatorPersonId == person.Id).ToList();

        // Assert
        Assert.AreEqual(1, metrics.Count);
        Assert.AreEqual("SemanticScholar", metrics[0].Source);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Metric_AccessibleViaNavigationProperty()
    {
        // Arrange
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Navigation Test",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var metric = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            HIndex = 10,
            CitationCount = 500
        };
        Context.InvestigatorMetrics.Add(metric);
        await Context.SaveChangesAsync();

        // Act: Clear change tracker and reload with navigation
        Context.ChangeTracker.Clear();
        var reloaded = Context.InvestigatorPersons
            .Where(p => p.Id == person.Id)
            .Select(p => new { person = p, metricCount = p.Metrics!.Count })
            .FirstOrDefault();

        // Assert
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(1, reloaded.metricCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Metric_WithNullOptionalFields()
    {
        // Arrange: metric with minimal data (common for "not_found" results)
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Unknown Researcher",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var metric = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            LookupResult = "not_found",
            LookupAttemptedAt = DateTime.UtcNow
            // All metric fields are null
        };
        Context.InvestigatorMetrics.Add(metric);
        await Context.SaveChangesAsync();

        // Act
        var saved = await Context.InvestigatorMetrics.FindAsync(metric.Id);

        // Assert
        Assert.IsNotNull(saved);
        Assert.AreEqual("not_found", saved.LookupResult);
        Assert.IsNull(saved.HIndex);
        Assert.IsNull(saved.CitationCount);
        Assert.IsNull(saved.I10Index);
        Assert.IsNull(saved.TotalPapers);
        Assert.IsNull(saved.ExternalAuthorId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Metric_UniqueConstraintOnSourcePerInvestigator()
    {
        // Arrange
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Unique Test",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var metric1 = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            HIndex = 10
        };
        Context.InvestigatorMetrics.Add(metric1);
        await Context.SaveChangesAsync();

        // Act & Assert: Try to add another SemanticScholar metric for same person
        var metric2 = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            HIndex = 20
        };
        Context.InvestigatorMetrics.Add(metric2);

        // Saving should throw due to unique constraint
        var ex = await Assert.ThrowsExceptionAsync<DbUpdateException>(() => Context.SaveChangesAsync());
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Metric_LookupAttemptedAt_PreventsDuplicateLookups()
    {
        // Arrange
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Idempotent",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var attemptTime = DateTime.UtcNow.AddHours(-1);
        var metric = new InvestigatorMetricEntity
        {
            InvestigatorPersonId = person.Id,
            Source = "SemanticScholar",
            HIndex = 5,
            CitationCount = 100,
            LookupAttemptedAt = attemptTime,
            LookupResult = "found"
        };
        Context.InvestigatorMetrics.Add(metric);
        await Context.SaveChangesAsync();

        // Act: Verify lookup was already attempted
        Context.ChangeTracker.Clear();
        var existing = Context.InvestigatorMetrics
            .Where(m => m.InvestigatorPersonId == person.Id && m.Source == "SemanticScholar")
            .ToList();

        // Assert
        Assert.AreEqual(1, existing.Count);
        Assert.IsNotNull(existing[0].LookupAttemptedAt);
        Assert.IsTrue(existing[0].LookupAttemptedAt <= DateTime.UtcNow);
    }
}
