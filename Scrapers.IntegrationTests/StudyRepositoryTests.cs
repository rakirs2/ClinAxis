using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
                [
                    new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" },
                    new Investigator { Name = "Bob Jones", Affiliation = "Acme Research", Role = "SUB_INVESTIGATOR" }
                ]),
            CreateRecord("NCT00000002", "Study Two", "COMPLETED",
                [new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" }])
        ];

        var ingested = await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        Assert.AreEqual(records.Length, ingested);
        Assert.AreEqual(records.Length, await _repo.CountStudiesAsync());
        Assert.AreEqual(3, await _repo.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_IsIdempotent()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            [new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" }]);

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);
        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        Assert.AreEqual(1, await _repo.CountStudiesAsync());
        Assert.AreEqual(1, await _repo.CountInvestigatorsAsync());
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByTitleSubstring()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00999999", "Pregabalin for Neuropathic Pain Relief Trial", "COMPLETED",
            [new Investigator { Name = "Eve Adams", Affiliation = "Pain Clinic", Role = "PRINCIPAL_INVESTIGATOR" }]);
        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        IReadOnlyList<StudyEntity> results = await _repo.GetStudiesPagedAsync(1, 10, search: "pregabalin");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00999999", results[0].NctId);

        IReadOnlyList<StudyEntity> noResults = await _repo.GetStudiesPagedAsync(1, 10, search: "zzzznotfound");
        Assert.AreEqual(0, noResults.Count);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_WithTwoStudies_OnlyPregabalinMatches()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00001001", "Pregabalin for Neuropathic Pain Relief Trial", "COMPLETED",
                [new Investigator { Name = "Eve Adams", Affiliation = "Pain Clinic", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00001002", "Aspirin Cardiovascular Prevention Study", "RECRUITING",
                [new Investigator { Name = "Frank Lee", Role = "PRINCIPAL_INVESTIGATOR" }]),
        ];
        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> results = await _repo.GetStudiesPagedAsync(1, 10, search: "Pregabalin");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00001001", results[0].NctId);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByStatusFilter()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000101", "Study Alpha", "RECRUITING",
                [new Investigator { Name = "Frank Lee", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00000102", "Study Beta", "COMPLETED",
                [new Investigator { Name = "Grace Kim", Role = "PRINCIPAL_INVESTIGATOR" }]),
        ];
        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> recruiting = await _repo.GetStudiesPagedAsync(1, 10, status: "RECRUITING");
        Assert.AreEqual(1, recruiting.Count);
        Assert.AreEqual("NCT00000101", recruiting[0].NctId);

        IReadOnlyList<StudyEntity> completed = await _repo.GetStudiesPagedAsync(1, 10, status: "COMPLETED");
        Assert.AreEqual(1, completed.Count);
        Assert.AreEqual("NCT00000102", completed[0].NctId);
    }

    [TestMethod]
    public async Task SearchStudiesAsync_FindsByMultipleStatuses()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000201", "Study Gamma", "RECRUITING",
                [new Investigator { Name = "Henry Ford", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00000202", "Study Delta", "ACTIVE",
                [new Investigator { Name = "Iris Chang", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00000203", "Study Epsilon", "COMPLETED",
                [new Investigator { Name = "Jack Brown", Role = "PRINCIPAL_INVESTIGATOR" }]),
        ];
        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        IReadOnlyList<StudyEntity> activeOrRecruiting = await _repo.GetStudiesPagedAsync(1, 10, status: "RECRUITING,ACTIVE");
        Assert.AreEqual(2, activeOrRecruiting.Count);
    }

    [TestMethod]
    public async Task UpdateStudies_StoresReferences()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00000401", "Reference Test Study", "RECRUITING",
            [new Investigator { Name = "Kim Lee", Role = "PRINCIPAL_INVESTIGATOR" }],
            [
                new ClinicalTrialRecord.Reference { Pmid = "12345678", Citation = "Test Citation 1", Type = "BACKGROUND" },
                new ClinicalTrialRecord.Reference { Pmid = "87654321", Citation = "Test Citation 2", Type = "RESULT" }
            ]);

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        List<StudyReferenceEntity> refs = await Context.StudyReferences
            .Where(r => r.StudyNctId == "NCT00000401")
            .OrderBy(r => r.Id)
            .ToListAsync();

        Assert.AreEqual(2, refs.Count);
        Assert.AreEqual("12345678", refs[0].Pmid);
        Assert.AreEqual("Test Citation 1", refs[0].Citation);
        Assert.AreEqual("BACKGROUND", refs[0].Type);
        Assert.AreEqual("87654321", refs[1].Pmid);
    }

    [TestMethod]
    public async Task UpdateStudies_StoresReferencesForIncompleteStudies()
    {
        // Study with no investigators should be marked incomplete but still store references
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000402",
            BriefTitle = "Incomplete With References",
            OverallStatus = "UNKNOWN",
            OverallOfficials = null,
            References =
            [
                new ClinicalTrialRecord.Reference { Pmid = "55555555", Citation = "Orphan Reference", Type = "BACKGROUND" }
            ]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        StudyEntity? study = await Context.Studies
            .Include(s => s.References)
            .FirstOrDefaultAsync(s => s.NctId == "NCT00000402");

        Assert.IsNotNull(study);
        Assert.IsTrue(study.IsIncomplete);
        Assert.IsNotNull(study.References);
        Assert.AreEqual(1, study.References!.Count);
        Assert.AreEqual("55555555", study.References!.First().Pmid);
    }

    [TestMethod]
    public async Task UpdateStudies_ReferencesAreIdempotent()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00000403", "Reference Idempotency", "RECRUITING",
            [new Investigator { Name = "Mia Torres", Role = "PRINCIPAL_INVESTIGATOR" }],
            [
                new ClinicalTrialRecord.Reference { Pmid = "11111111", Citation = "First ref", Type = "BACKGROUND" },
                new ClinicalTrialRecord.Reference { Pmid = "22222222", Citation = "Second ref", Type = "RESULT" }
            ]);

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        // Re-ingest with same study but different references
        ClinicalTrialRecord updatedRecord = CreateRecord("NCT00000403", "Reference Idempotency", "RECRUITING",
            [new Investigator { Name = "Mia Torres", Role = "PRINCIPAL_INVESTIGATOR" }],
            [
                new ClinicalTrialRecord.Reference { Pmid = "33333333", Citation = "Replaced ref", Type = "RESULT" }
            ]);

        await _repo.UpdateStudiesWithClinicalTrialsAsync([updatedRecord]);

        List<StudyReferenceEntity> refs = await Context.StudyReferences
            .Where(r => r.StudyNctId == "NCT00000403")
            .OrderBy(r => r.Id)
            .ToListAsync();

        Assert.AreEqual(1, refs.Count, "Old references should be cleared and replaced.");
        Assert.AreEqual("33333333", refs[0].Pmid);
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status,
        Investigator[]? investigators, List<ClinicalTrialRecord.Reference>? references = null)
    {
        return new ClinicalTrialRecord
        {
            NctId = nctId,
            BriefTitle = title,
            OverallStatus = status,
            OverallOfficials = investigators?.ToList(),
            References = references
        };
    }
}
