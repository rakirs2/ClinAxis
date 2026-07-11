using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class ClinicalTrialsIngestionIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestionPersistsRequestedCount()
    {
        var repository = new StudyRepository(ConnectionString);
        var service = new ClinicalTrialsIngestionService(new ClinicalTrialsGov(), repository);

        var saved = await service.IngestAsync(5);
        var investigatorCount = await repository.CountInvestigatorsAsync();

        Assert.AreEqual(5, saved, "Ingestion should report five persisted studies.");
        Assert.AreEqual(5, await repository.CountStudiesAsync(), "Database should contain five studies after ingestion.");
        Assert.IsTrue(investigatorCount > 0, "Investigators table should have at least one row after ingestion.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestionIsIdempotent()
    {
        var repository = new StudyRepository(ConnectionString);
        var service = new ClinicalTrialsIngestionService(new ClinicalTrialsGov(), repository);

        var saved = await service.IngestAsync(5);
        var investigatorCount = await repository.CountInvestigatorsAsync();

        var savedAgain = await service.IngestAsync(5);
        Assert.AreEqual(5, savedAgain, "Re-ingestion should still process five studies.");
        Assert.AreEqual(5, await repository.CountStudiesAsync(), "Re-ingestion should not duplicate studies.");
        Assert.AreEqual(investigatorCount, await repository.CountInvestigatorsAsync(), "Investigator count should remain stable after re-ingestion.");
    }
}
