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

    [TestMethod]
    public async Task UpsertStudiesAsync_CreatesNormalizedInvestigatorPersons()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT01000001", "Study Alpha", "RECRUITING",
                [
                    new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" },
                    new Investigator { Name = "Bob Jones", Affiliation = "Acme Research", Role = "SUB_INVESTIGATOR" }
                ]),
            CreateRecord("NCT01000002", "Study Beta", "COMPLETED",
                [new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" }])
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        List<InvestigatorPersonEntity> persons = await Context.InvestigatorPersons
            .OrderBy(p => p.FullName)
            .ToListAsync();

        Assert.AreEqual(3, persons.Count, "Should create one person per unique name");
        Assert.AreEqual("Alice Smith", persons[0].FullName);
        Assert.AreEqual("Bob Jones", persons[1].FullName);
        Assert.AreEqual("Carol White", persons[2].FullName);
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_DeduplicatesInvestigatorPersonsByName()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT01000003", "Study Gamma", "RECRUITING",
                [new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT01000004", "Study Delta", "COMPLETED",
                [new Investigator { Name = "Alice Smith", Affiliation = "Different Hospital", Role = "PRINCIPAL_INVESTIGATOR" }])
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        List<InvestigatorPersonEntity> persons = await Context.InvestigatorPersons.ToListAsync();
        Assert.AreEqual(1, persons.Count, "Same name should map to one person record");
        Assert.AreEqual("Alice Smith", persons[0].FullName);

        List<StudyInvestigatorEntity> junctions = await Context.StudyInvestigators.ToListAsync();
        Assert.AreEqual(2, junctions.Count, "Two studies means two junction rows");
        Assert.IsTrue(junctions.All(j => j.InvestigatorPersonId == persons[0].Id),
            "Both junctions should reference the same person");
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_EnqueuesInvestigatorDiscoveredEventForNewPerson()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT02000001", "New PI Study", "RECRUITING",
                [new Investigator { Name = "Dr. Eve NewPerson, PhD", Affiliation = "New Lab", Role = "PRINCIPAL_INVESTIGATOR" }])
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        List<PipelineEventEntity> events = await Context.PipelineEvents
            .Where(e => e.EventType == "investigator.discovered")
            .ToListAsync();
        Assert.AreEqual(1, events.Count, "Should enqueue one investigator.discovered event");
        Assert.AreEqual("pending", events[0].Status);
    }

    [TestMethod]
    public async Task ScrubInvestigatorAsync_CreatesInvestigatorPaperLinks()
    {
        var personId = Guid.NewGuid();
        var paperId = Guid.NewGuid();
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = personId,
            FullName = "Dr. Marie Curie",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.Studies.Add(new StudyEntity
        {
            NctId = "NCT03000001",
            BriefTitle = "Radium Study",
            OverallStatus = "COMPLETED",
            CreatedAt = DateTime.UtcNow
        });
        Context.StudyInvestigators.Add(new StudyInvestigatorEntity
        {
            StudyNctId = "NCT03000001",
            InvestigatorPersonId = personId,
            RoleOnStudy = "PI",
            IsOverallOfficial = true
        });
        Context.PubmedPapers.Add(new PubmedPaperEntity
        {
            Id = paperId,
            Pmid = "99999999",
            Title = "Radium Discovery Paper",
            Journal = "Nature",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.StudyReferences.Add(new StudyReferenceEntity
        {
            StudyNctId = "NCT03000001",
            Pmid = "99999999",
            Citation = "Marie Curie et al. Nature.",
            Type = "PubMed"
        });
        await Context.SaveChangesAsync();

        // Verify the study reference was created
        var refCount = await Context.StudyReferences
            .Where(r => r.StudyNctId == "NCT03000001")
            .CountAsync();
        Assert.AreEqual(1, refCount, "Should have one study reference");

        await StudyRepository.ScrubInvestigatorPapersAsync(
            Context, personId, default);

        List<InvestigatorPaperEntity> links = await Context.InvestigatorPapers
            .Where(ip => ip.InvestigatorPersonId == personId)
            .ToListAsync();
        Assert.AreEqual(1, links.Count, "Should create one investigator-paper link");
        Assert.AreEqual(paperId, links[0].PubmedPaperId);
    }

    [TestMethod]
    public async Task IngestStudy_WithNonPersonOfficial_MarksIncomplete()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000901",
            BriefTitle = "Pharma Sponsored Study",
            OverallStatus = "ACTIVE",
            Keywords = ["cancer", "chemotherapy"],
            OverallOfficials =
            [
                new Investigator { Name = "Pfizer", Role = null },
                new Investigator { Name = "Dr. Alice Smith, MD", Role = "PRINCIPAL_INVESTIGATOR" },
            ]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        StudyEntity? study = await Context.Studies
            .Include(s => s.StudyInvestigators!)
                .ThenInclude(si => si.InvestigatorPerson)
            .FirstOrDefaultAsync(s => s.NctId == "NCT00000901");

        Assert.IsNotNull(study);
        Assert.IsFalse(study.IsIncomplete, "Study should not be incomplete (has at least one valid PI)");

        var person = study.StudyInvestigators!
            .Select(si => si.InvestigatorPerson)
            .FirstOrDefault();

        Assert.IsNotNull(person);
        Assert.AreEqual("Alice Smith", person.FullName, "Name should be parsed, honorifics stripped");
        Assert.AreEqual("Dr.", person.Prefix, "Prefix should be preserved");

        // Pfizer should not have been created as a person
        var allPersons = await Context.InvestigatorPersons.ToListAsync();
        Assert.IsFalse(allPersons.Any(p => p.FullName.Contains("Pfizer", System.StringComparison.OrdinalIgnoreCase)),
            "Pfizer should not be stored as an investigator person");
    }

    [TestMethod]
    public async Task IngestStudy_WithAllNonPersonOfficials_MarksIncomplete()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000902",
            BriefTitle = "Industry Sponsored Study",
            OverallStatus = "ACTIVE",
            OverallOfficials =
            [
                new Investigator { Name = "University of California", Role = null },
                new Investigator { Name = "Roche", Role = null },
            ]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        StudyEntity? study = await Context.Studies
            .FirstOrDefaultAsync(s => s.NctId == "NCT00000902");

        Assert.IsNotNull(study);
        Assert.IsTrue(study.IsIncomplete, "Study should be marked incomplete when all officials are non-human");
    }

    [TestMethod]
    public async Task IngestStudy_DeduplicatesAndFiltersKeywords()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000903",
            BriefTitle = "Keyword Filter Test",
            OverallStatus = "RECRUITING",
            Conditions = ["cancer"],
            Keywords = ["cancer", "cancer", "lung cancer", "l", "HIV",
                "A very long keyword that exceeds two hundred characters so it should be rejected by the keyword filter because it is way too long and not useful for search purposes at all and should not be stored in the database"],
            OverallOfficials = [new Investigator { Name = "Test Researcher", Role = "PRINCIPAL_INVESTIGATOR" }]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var keywords = await Context.StudyKeywords
            .Where(k => k.StudyNctId == "NCT00000903")
            .Select(k => k.Keyword)
            .ToListAsync();

        // "cancer" is a condition duplicate → skipped
        // "cancer" appears twice → deduped
        // "l" is too short and not a known medical term → skipped
        // HIV is 3 chars but is known medical term → kept
        // long keyword > 200 chars → skipped
        Assert.AreEqual(2, keywords.Count, "Only 'lung cancer' and 'HIV' should remain");
        Assert.IsTrue(keywords.Contains("lung cancer"));
        Assert.IsTrue(keywords.Contains("HIV"));
    }

    [TestMethod]
    public async Task CountStudiesByInvestigatorPersonIdAsync_ReturnsCorrectTotal()
    {
        var investigatorName = "Dr. Jane Doe";
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00001001", "Study Alpha", "RECRUITING",
                [new Investigator { Name = investigatorName, Affiliation = "Med Corp", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00001002", "Study Beta", "COMPLETED",
                [new Investigator { Name = investigatorName, Affiliation = "Med Corp", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00001003", "Study Gamma", "TERMINATED",
                [new Investigator { Name = investigatorName, Affiliation = "Med Corp", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00001004", "Other Study", "RECRUITING",
                [new Investigator { Name = "Other Person", Affiliation = "Other Corp", Role = "PRINCIPAL_INVESTIGATOR" }])
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        var personId = await Context.InvestigatorPersons
            .Where(p => p.FullName == "Jane Doe")
            .Select(p => p.Id)
            .FirstAsync();

        var criteria = new StudySearchCriteria { Page = 1, PageSize = 20 };
        var total = await _repo.CountStudiesByInvestigatorPersonIdAsync(personId, criteria);

        Assert.AreEqual(3, total, "Investigator with 3 studies should return count of 3");
    }

    [TestMethod]
    public async Task CountStudiesByInvestigatorPersonIdAsync_WithFilter_ReturnsFilteredTotal()
    {
        var investigatorName = "Dr. John Smith";
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00002001", "Cancer Research Alpha", "RECRUITING",
                [new Investigator { Name = investigatorName, Affiliation = "Research Co", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00002002", "Heart Study Beta", "COMPLETED",
                [new Investigator { Name = investigatorName, Affiliation = "Research Co", Role = "PRINCIPAL_INVESTIGATOR" }]),
            CreateRecord("NCT00002003", "Cancer Research Gamma", "TERMINATED",
                [new Investigator { Name = investigatorName, Affiliation = "Research Co", Role = "PRINCIPAL_INVESTIGATOR" }])
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        var personId = await Context.InvestigatorPersons
            .Where(p => p.FullName == "John Smith")
            .Select(p => p.Id)
            .FirstAsync();

        var criteria = new StudySearchCriteria
        {
            Page = 1,
            PageSize = 20,
            Keyword = "cancer"
        };
        var total = await _repo.CountStudiesByInvestigatorPersonIdAsync(personId, criteria);

        Assert.AreEqual(2, total, "Filtering by 'cancer' should return 2 studies");
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
