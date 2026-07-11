using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class StudyRepositoryTests : DbTestBase
{
    [TestMethod]
    public async Task UpsertStudiesAsync_PersistsStudiesAndInvestigators()
    {
        var repository = new StudyRepository(ConnectionString);

        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000001", "Study One", "RECRUITING",
                new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" },
                new Investigator { Name = "Bob Jones", Affiliation = "Acme Research", Role = "SUB_INVESTIGATOR" }),
            CreateRecord("NCT00000002", "Study Two", "COMPLETED",
                new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" })
        ];

        var ingested = await repository.UpdateStudiesWithClinicalTrialsAsync(records);

        Assert.AreEqual(records.Length, ingested);
        Assert.AreEqual(records.Length, await repository.CountStudiesAsync());
        Assert.AreEqual(3, await repository.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_IsIdempotent()
    {
        var repository = new StudyRepository(ConnectionString);

        ClinicalTrialRecord record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" });

        await repository.UpdateStudiesWithClinicalTrialsAsync([record]);
        await repository.UpdateStudiesWithClinicalTrialsAsync([record]);

        Assert.AreEqual(1, await repository.CountStudiesAsync());
        Assert.AreEqual(1, await repository.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByTitleSubstring()
    {
        var repository = new StudyRepository(ConnectionString);

        ClinicalTrialRecord record = CreateRecord("NCT00999999", "Pregabalin for Neuropathic Pain Relief Trial", "COMPLETED",
            new Investigator { Name = "Eve Adams", Affiliation = "Pain Clinic", Role = "PRINCIPAL_INVESTIGATOR" });
        await repository.UpdateStudiesWithClinicalTrialsAsync([record]);

        IReadOnlyList<StudyEntity> results = await repository.GetStudiesPagedAsync(1, 10, search: "pregabalin");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00999999", results[0].NctId);

        IReadOnlyList<StudyEntity> noResults = await repository.GetStudiesPagedAsync(1, 10, search: "zzzznotfound");
        Assert.AreEqual(0, noResults.Count);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByStatusFilter()
    {
        var repository = new StudyRepository(ConnectionString);

        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000101", "Study Alpha", "RECRUITING",
                new Investigator { Name = "Frank Lee", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000102", "Study Beta", "COMPLETED",
                new Investigator { Name = "Grace Kim", Role = "PRINCIPAL_INVESTIGATOR" }),
        ];
        await repository.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> recruiting = await repository.GetStudiesPagedAsync(1, 10, status: "RECRUITING");
        Assert.AreEqual(1, recruiting.Count);
        Assert.AreEqual("NCT00000101", recruiting[0].NctId);

        IReadOnlyList<StudyEntity> completed = await repository.GetStudiesPagedAsync(1, 10, status: "COMPLETED");
        Assert.AreEqual(1, completed.Count);
        Assert.AreEqual("NCT00000102", completed[0].NctId);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByMultipleStatuses()
    {
        var repository = new StudyRepository(ConnectionString);

        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000201", "Study Gamma", "RECRUITING",
                new Investigator { Name = "Henry Ford", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000202", "Study Delta", "ACTIVE",
                new Investigator { Name = "Iris Chang", Role = "PRINCIPAL_INVESTIGATOR" }),
            CreateRecord("NCT00000203", "Study Epsilon", "COMPLETED",
                new Investigator { Name = "Jack Brown", Role = "PRINCIPAL_INVESTIGATOR" }),
        ];
        await repository.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> activeOrRecruiting = await repository.GetStudiesPagedAsync(1, 10, status: "RECRUITING,ACTIVE");
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
