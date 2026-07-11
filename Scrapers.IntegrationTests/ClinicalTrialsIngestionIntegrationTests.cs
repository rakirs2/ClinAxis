using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class ClinicalTrialsIngestionIntegrationTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestionPersistsRequestedCount()
    {
        var service = new ClinicalTrialsIngestionService(new ClinicalTrialsGov(), _repo);

        var saved = await service.IngestAsync(5);
        var investigatorCount = await _repo.CountInvestigatorsAsync();

        Assert.AreEqual(5, saved, "Ingestion should report five persisted studies.");
        Assert.AreEqual(5, await _repo.CountStudiesAsync(), "Database should contain five studies after ingestion.");
        Assert.IsTrue(investigatorCount > 0, "Investigators table should have at least one row after ingestion.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestionIsIdempotent()
    {
        var service = new ClinicalTrialsIngestionService(new ClinicalTrialsGov(), _repo);

        var saved = await service.IngestAsync(5);
        var investigatorCount = await _repo.CountInvestigatorsAsync();

        var savedAgain = await service.IngestAsync(5);
        Assert.AreEqual(5, savedAgain, "Re-ingestion should still process five studies.");
        Assert.AreEqual(5, await _repo.CountStudiesAsync(), "Re-ingestion should not duplicate studies.");
        Assert.AreEqual(investigatorCount, await _repo.CountInvestigatorsAsync(), "Investigator count should remain stable after re-ingestion.");
    }
}
