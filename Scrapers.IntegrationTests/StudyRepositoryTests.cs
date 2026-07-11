using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class StudyRepositoryTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_PersistsStudiesAndInvestigators()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000001", "Study One", "RECRUITING",
                new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" },
                new Investigator { Name = "Bob Jones", Affiliation = "Acme Research", Role = "SUB_INVESTIGATOR" }),
            CreateRecord("NCT00000002", "Study Two", "COMPLETED",
                new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" })
        ];

        var ingested = await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        Assert.AreEqual(records.Length, ingested);
        Assert.AreEqual(records.Length, await _repo.CountStudiesAsync());
        Assert.AreEqual(3, await _repo.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_IsIdempotent()
    {
        var record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" });

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);
        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        Assert.AreEqual(1, await _repo.CountStudiesAsync());
        Assert.AreEqual(1, await _repo.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByTitleSubstring()
    {
        var record = CreateRecord("NCT00999999", "Pregabalin for Neuropathic Pain Relief Trial", "COMPLETED",
            new Investigator { Name = "Eve Adams", Affiliation = "Pain Clinic", Role = "PRINCIPAL_INVESTIGATOR" });
        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var results = await _repo.GetStudiesPagedAsync(1, 10, search: "pregabalin");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00999999", results[0].NctId);

        var noResults = await _repo.GetStudiesPagedAsync(1, 10, search: "zzzznotfound");
        Assert.AreEqual(0, noResults.Count);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByStatusFilter()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000101", "Study Alpha", "RECRUITING",
                new Investigator { Name = "Frank Lee", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000102", "Study Beta", "COMPLETED",
                new Investigator { Name = "Grace Kim", Role = "PRINCIPAL_INVESTIGATOR" }),
        ];
        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        var recruiting = await _repo.GetStudiesPagedAsync(1, 10, status: "RECRUITING");
        Assert.AreEqual(1, recruiting.Count);
        Assert.AreEqual("NCT00000101", recruiting[0].NctId);

        var completed = await _repo.GetStudiesPagedAsync(1, 10, status: "COMPLETED");
        Assert.AreEqual(1, completed.Count);
        Assert.AreEqual("NCT00000102", completed[0].NctId);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByMultipleStatuses()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000201", "Study Gamma", "RECRUITING",
                new Investigator { Name = "Henry Ford", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000202", "Study Delta", "ACTIVE",
                new Investigator { Name = "Iris Chang", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000203", "Study Epsilon", "COMPLETED",
                new Investigator { Name = "Jack Brown", Role = "PRINCIPAL_INVESTIGATOR" }),
        ];
        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        var activeOrRecruiting = await _repo.GetStudiesPagedAsync(1, 10, status: "RECRUITING,ACTIVE");
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
