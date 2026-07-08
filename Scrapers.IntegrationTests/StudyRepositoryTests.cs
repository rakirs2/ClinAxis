using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.IntegrationTests.Helpers;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class StudyRepositoryTests
{
    private StudyRepository _repository = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _repository = new StudyRepository(PostgresTestHelper.ConnectionString);
        await _repository.EnsureSchemaAsync();
        await PostgresTestHelper.ClearDatabaseAsync();
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_PersistsStudiesAndInvestigators()
    {
        var records = new[]
        {
            CreateRecord("NCT00000001", "Study One", "RECRUITING",
                new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" },
                new Investigator { Name = "Bob Jones", Affiliation = "Acme Research", Role = "SUB_INVESTIGATOR" }),
            CreateRecord("NCT00000002", "Study Two", "COMPLETED",
                new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" })
        };

        var ingested = await _repository.UpdateStudiesWithClinicalTrialsAsync(records);

        Assert.AreEqual(records.Length, ingested);
        Assert.AreEqual(records.Length, await _repository.CountStudiesAsync());
        Assert.AreEqual(3, await _repository.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_IsIdempotent()
    {
        var record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" });

        await _repository.UpdateStudiesWithClinicalTrialsAsync(new[] { record });
        await _repository.UpdateStudiesWithClinicalTrialsAsync(new[] { record });

        Assert.AreEqual(1, await _repository.CountStudiesAsync());
        Assert.AreEqual(1, await _repository.CountInvestigatorsAsync());
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status, params Investigator[] investigators)
    {
        return new ClinicalTrialRecord
        {
            NctId = nctId,
            BriefTitle = title,
            OverallStatus = status,
            OverallOfficials = investigators.ToList()
        };
    }
}