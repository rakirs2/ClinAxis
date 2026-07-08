using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class ClinicalTrialsIngestionIntegrationTests
{
    private ClinicalTrialsIngestionService _service = null!;
    private PostgresStudyRepository _repository = null!;
    private int _saved;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _repository = new PostgresStudyRepository(PostgresTestHelper.ConnectionString);
        _service = new ClinicalTrialsIngestionService(new ClinicalTrialsGov(), _repository);

        await _repository.EnsureSchemaAsync();
        await PostgresTestHelper.ClearDatabaseAsync();
        _saved = await _service.IngestAsync(5);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestionPersistsRequestedCount()
    {
        Assert.AreEqual(5, _saved, "Ingestion should report five persisted studies.");
        Assert.AreEqual(5, await _repository.CountStudiesAsync(), "Database should contain five studies after ingestion.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestionIsIdempotent()
    {
        var savedAgain = await _service.IngestAsync(5);
        Assert.AreEqual(5, savedAgain, "Re-ingestion should still process five studies.");
        Assert.AreEqual(5, await _repository.CountStudiesAsync(), "Re-ingestion should not duplicate studies.");
    }
}
