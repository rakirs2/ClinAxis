using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
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
        ClinicalTrialRecord[] records = new[]
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
        ClinicalTrialRecord record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" });

        await _repository.UpdateStudiesWithClinicalTrialsAsync(new[] { record });
        await _repository.UpdateStudiesWithClinicalTrialsAsync(new[] { record });

        Assert.AreEqual(1, await _repository.CountStudiesAsync());
        Assert.AreEqual(1, await _repository.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByTitleSubstring()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00999999", "Pregabalin for Neuropathic Pain Relief Trial", "COMPLETED",
            new Investigator { Name = "Eve Adams", Affiliation = "Pain Clinic", Role = "PRINCIPAL_INVESTIGATOR" });
        await _repository.UpdateStudiesWithClinicalTrialsAsync(new[] { record });

        IReadOnlyList<StudyEntity> results = await _repository.GetStudiesPagedAsync(1, 10, search: "pregabalin");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00999999", results[0].NctId);

        IReadOnlyList<StudyEntity> noResults = await _repository.GetStudiesPagedAsync(1, 10, search: "zzzznotfound");
        Assert.AreEqual(0, noResults.Count);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByStatusFilter()
    {
        ClinicalTrialRecord[] records = new[]
        {
            CreateRecord("NCT00000101", "Study Alpha", "RECRUITING",
                new Investigator { Name = "Frank Lee", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000102", "Study Beta", "COMPLETED",
                new Investigator { Name = "Grace Kim", Role = "PRINCIPAL_INVESTIGATOR" }),
        };
        await _repository.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> recruiting = await _repository.GetStudiesPagedAsync(1, 10, status: "RECRUITING");
        Assert.AreEqual(1, recruiting.Count);
        Assert.AreEqual("NCT00000101", recruiting[0].NctId);

        IReadOnlyList<StudyEntity> completed = await _repository.GetStudiesPagedAsync(1, 10, status: "COMPLETED");
        Assert.AreEqual(1, completed.Count);
        Assert.AreEqual("NCT00000102", completed[0].NctId);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByMultipleStatuses()
    {
        ClinicalTrialRecord[] records = new[]
        {
            CreateRecord("NCT00000201", "Study Gamma", "RECRUITING",
                new Investigator { Name = "Henry Ford", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000202", "Study Delta", "ACTIVE",
                new Investigator { Name = "Iris Chang", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000203", "Study Epsilon", "COMPLETED",
                new Investigator { Name = "Jack Brown", Role = "PRINCIPAL_INVESTIGATOR" }),
        };
        await _repository.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> activeOrRecruiting = await _repository.GetStudiesPagedAsync(1, 10, status: "RECRUITING,ACTIVE");
        Assert.AreEqual(2, activeOrRecruiting.Count);
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
