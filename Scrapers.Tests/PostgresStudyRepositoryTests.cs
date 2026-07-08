using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class PostgresStudyRepositoryTests
{
    private PostgresStudyRepository _repository = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _repository = new PostgresStudyRepository(PostgresTestHelper.ConnectionString);
        await _repository.EnsureSchemaAsync();
        await PostgresTestHelper.ClearDatabaseAsync();
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_PersistsStudiesAndInvestigators()
    {
        var records = new[]
        {
            CreateRecord("NCT00000001", "Study One", "RECRUITING",
                new Investigator("Alice Smith", "Acme Research", "PRINCIPAL_INVESTIGATOR"),
                new Investigator("Bob Jones", "Acme Research", "SUB_INVESTIGATOR")),
            CreateRecord("NCT00000002", "Study Two", "COMPLETED",
                new Investigator("Carol White", "Health Org", "STUDY_DIRECTOR"))
        };

        var ingested = await _repository.UpsertStudiesAsync(records);

        Assert.AreEqual(records.Length, ingested);
        Assert.AreEqual(records.Length, await _repository.CountStudiesAsync());
        Assert.AreEqual(3, await _repository.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_IsIdempotent()
    {
        var record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            new Investigator("Dana King", "Wellness Org", "STUDY_DIRECTOR"));

        await _repository.UpsertStudiesAsync(new[] { record });
        await _repository.UpsertStudiesAsync(new[] { record });

        Assert.AreEqual(1, await _repository.CountStudiesAsync());
        Assert.AreEqual(1, await _repository.CountInvestigatorsAsync());
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status, params Investigator[] investigators)
    {
        var summary = new StudySummary(nctId, title, status);
        return new ClinicalTrialRecord(summary, investigators);
    }

}
