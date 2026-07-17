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
    public async Task UpsertStudiesAsync_EnqueuesInvestigatorEnrichmentEventForNewPerson()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT02000001", "New PI Study", "RECRUITING",
                [new Investigator { Name = "Dr. Eve NewPerson, PhD", Affiliation = "New Lab", Role = "PRINCIPAL_INVESTIGATOR" }])
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        List<PipelineEventEntity> events = await Context.PipelineEvents
            .Where(e => e.EventType == "investigator.enrichment")
            .ToListAsync();
        Assert.AreEqual(1, events.Count, "Should enqueue one investigator.enrichment event");
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
            Keywords =
            [
                "cancer", "cancer",                          // condition duplicate + dup → removed
                "lung cancer",                                // valid → kept (lowercased)
                "l",                                          // too short, not known medical term → removed
                "HIV",                                        // 3 chars but in knownShortMedicalTerms → kept
                "A very long keyword over one hundred fifty characters that should be rejected by the filter because it is way too long for a keyword",
                "treatment",                                  // keywordBlocklist match → removed
                "healthy subjects",                           // keywordBlocklist match → removed
                "safety",                                     // keywordBlocklist match → removed
                "diabetes; obesity",                          // contains semicolon → removed
                "clinical trial",                             // keywordBlocklist match → removed
                "cancer, ",                                   // trailing comma normalized to "cancer" → condition dup → removed
                "Parkinson's disease",                        // valid → kept (lowercased)
                "EXERCISE",                                   // case normalization → "exercise" → kept
                "a|b|c",                                      // contains pipe → removed
                "stroke, hippotherapy, balance, postural control, gait",  // 4 commas → removed
                "diagnosis",                                  // keywordBlocklist match → removed
                "therapy",                                    // keywordBlocklist match → removed
            ],
            OverallOfficials = [new Investigator { Name = "Test Researcher", Role = "PRINCIPAL_INVESTIGATOR" }]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var keywords = await Context.StudyKeywords
            .Where(k => k.StudyNctId == "NCT00000903")
            .Select(k => k.Keyword)
            .ToListAsync();

        Assert.AreEqual(4, keywords.Count, "Only 'LUNG CANCER', 'HIV', 'PARKINSON'S DISEASE', and 'EXERCISE' should remain");
        Assert.IsTrue(keywords.Contains("LUNG CANCER"));
        Assert.IsTrue(keywords.Contains("HIV"));
        Assert.IsTrue(keywords.Contains("PARKINSON'S DISEASE"));
        Assert.IsTrue(keywords.Contains("EXERCISE"));
    }

    [TestMethod]
    public async Task IngestStudy_RejectsBadAffiliations()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000904",
            BriefTitle = "Affiliation Validation Test",
            OverallStatus = "RECRUITING",
            Conditions = ["cancer"],
            OverallOfficials =
            [
                new Investigator { Name = "Alice Smith", Affiliation = "Cardiology Center", Role = "PRINCIPAL_INVESTIGATOR" },
                new Investigator { Name = "Bob Jones", Affiliation = "Anesthesiologist", Role = "SUB_INVESTIGATOR" },
                new Investigator { Name = "Carol White", Affiliation = "Professor", Role = "STUDY_DIRECTOR" },
                new Investigator { Name = "Dan Brown", Affiliation = "Surgeon", Role = "PRINCIPAL_INVESTIGATOR" },
            ]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var alice = await Context.InvestigatorPersons.FirstOrDefaultAsync(p => p.FullName == "Alice Smith");
        Assert.IsNotNull(alice);
        var aliceAffils = await Context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == alice.Id)
            .ToListAsync();
        Assert.AreEqual(1, aliceAffils.Count, "Alice's valid affiliation should be stored");
        Assert.AreEqual("Cardiology Center", aliceAffils[0].InstitutionName);

        var bob = await Context.InvestigatorPersons.FirstOrDefaultAsync(p => p.FullName == "Bob Jones");
        Assert.IsNotNull(bob);
        var bobAffils = await Context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == bob.Id)
            .ToListAsync();
        Assert.AreEqual(0, bobAffils.Count, "Bob's 'Anesthesiologist' (occupation) should be rejected");

        var carol = await Context.InvestigatorPersons.FirstOrDefaultAsync(p => p.FullName == "Carol White");
        Assert.IsNotNull(carol);
        var carolAffils = await Context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == carol.Id)
            .ToListAsync();
        Assert.AreEqual(0, carolAffils.Count, "Carol's 'Professor' (role) should be rejected");

        var rejected = await Context.RejectedEntities
            .Where(r => r.EntityType == "affiliation" && r.StudyNctId == "NCT00000904")
            .ToListAsync();
        Assert.AreEqual(3, rejected.Count, "Three affiliations should be rejected and logged");
        Assert.IsTrue(rejected.Any(r => r.Value == "Anesthesiologist"));
        Assert.IsTrue(rejected.Any(r => r.Value == "Professor"));
        Assert.IsTrue(rejected.Any(r => r.Value == "Surgeon"));
    }

    [TestMethod]
    public async Task IngestStudy_RejectsBadConditions()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000905",
            BriefTitle = "Condition Validation Test",
            OverallStatus = "RECRUITING",
            Conditions =
            [
                "Diabetes Mellitus",
                "Diabetes \"Type 2\"",
                "C.O.P.D.",
                "Cancer (C80)",
                "Hypertension",
                "E11.9",
            ],
            OverallOfficials = [new Investigator { Name = "Test Researcher", Role = "PRINCIPAL_INVESTIGATOR" }]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var stored = await Context.StudyConditions
            .Where(c => c.StudyNctId == "NCT00000905")
            .Select(c => c.Condition)
            .ToListAsync();

        Assert.AreEqual(2, stored.Count, "Only 'Diabetes Mellitus' and 'Hypertension' should be stored");
        Assert.IsTrue(stored.Contains("Diabetes Mellitus"));
        Assert.IsTrue(stored.Contains("Hypertension"));

        var rejected = await Context.RejectedEntities
            .Where(r => r.EntityType == "condition" && r.StudyNctId == "NCT00000905")
            .ToListAsync();
        Assert.AreEqual(4, rejected.Count, "Four conditions should be rejected and logged");
        Assert.IsTrue(rejected.Any(r => r.Value == "Diabetes \"Type 2\""), "Quotes should be rejected");
        Assert.IsTrue(rejected.Any(r => r.Value == "C.O.P.D."), "Periods should be rejected");
        Assert.IsTrue(rejected.Any(r => r.Value == "Cancer (C80)"), "ICD code in parenthetical should be rejected");
        Assert.IsTrue(rejected.Any(r => r.Value == "E11.9"), "ICD-10 code should be rejected");
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

    [TestMethod]
    public async Task CountInvestigatorsWithNpiAsync_ReturnsCorrectCount()
    {
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "John Smith",
            Npi = "1234567890",
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Jane Doe",
            Npi = null,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Non-Human Entity",
            Npi = "9999999999",
            IsHuman = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Assert.AreEqual(1, await _repo.CountInvestigatorsWithNpiAsync());
    }

    [TestMethod]
    public async Task CountInvestigatorsByEnrichmentResultAsync_ReturnsCorrectCounts()
    {
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Assigned Doc",
            NpiEnrichmentResult = "assigned",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Not Found Doc",
            NpiEnrichmentResult = "not_found",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Ambiguous Doc",
            NpiEnrichmentResult = "ambiguous",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Non-Human With Result",
            NpiEnrichmentResult = "assigned",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            IsHuman = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Assert.AreEqual(1, await _repo.CountInvestigatorsByEnrichmentResultAsync("assigned"));
        Assert.AreEqual(1, await _repo.CountInvestigatorsByEnrichmentResultAsync("not_found"));
        Assert.AreEqual(1, await _repo.CountInvestigatorsByEnrichmentResultAsync("ambiguous"));
    }

    [TestMethod]
    public async Task CountInvestigatorsNotAttemptedAsync_ReturnsCorrectCount()
    {
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Attempted Doc",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Not Attempted Doc",
            NpiLookupAttemptedAt = null,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Non-Human Not Attempted",
            NpiLookupAttemptedAt = null,
            IsHuman = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Assert.AreEqual(1, await _repo.CountInvestigatorsNotAttemptedAsync());
    }

    [TestMethod]
    public async Task CountInvestigatorsByEnrichmentResultAsync_UnknownResult_ReturnsZero()
    {
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Some Doc",
            NpiEnrichmentResult = null,
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Assert.AreEqual(0, await _repo.CountInvestigatorsByEnrichmentResultAsync("bogus_value"));
    }

    [TestMethod]
    public async Task UpsertStudiesAsync_PersistsAffiliationsFromCtGov()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT01000010", "Heart Study", "RECRUITING",
                [new Investigator { Name = "Alice Smith", Affiliation = "Cardiology Center", Role = "PRINCIPAL_INVESTIGATOR" }],
                startDate: new DateOnly(2024, 1, 15)),
            CreateRecord("NCT01000011", "Brain Study", "RECRUITING",
                [new Investigator { Name = "Alice Smith", Affiliation = "Neurology Institute", Role = "PRINCIPAL_INVESTIGATOR" }],
                startDate: new DateOnly(2025, 3, 1)),
            CreateRecord("NCT01000012", "Lung Study", "COMPLETED",
                [new Investigator { Name = "Alice Smith", Affiliation = "Cardiology Center", Role = "PRINCIPAL_INVESTIGATOR" }],
                startDate: new DateOnly(2023, 6, 1)),
            CreateRecord("NCT01000013", "Kidney Study", "ACTIVE",
                [new Investigator { Name = "Bob Jones", Affiliation = "Renal Associates", Role = "PRINCIPAL_INVESTIGATOR" }],
                startDate: new DateOnly(2025, 1, 1))
        ];

        var ingested = await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        Assert.AreEqual(records.Length, ingested);

        // Alice should have 2 affiliations
        var alice = await Context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.FullName == "Alice Smith");
        Assert.IsNotNull(alice);

        var aliceAffils = await Context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == alice.Id)
            .ToListAsync();
        Assert.AreEqual(2, aliceAffils.Count);

        // Cardiology Center: 2 studies, latest 2024-01-15
        var cardio = aliceAffils.FirstOrDefault(a => a.InstitutionName == "Cardiology Center");
        Assert.IsNotNull(cardio);
        Assert.AreEqual(new DateOnly(2024, 1, 15), cardio.StartDate);
        Assert.IsTrue(cardio.IsPrimary, "Cardiology Center should be primary (count=2 > Neurology's count=1)");

        // Neurology Institute: 1 study, latest 2025-03-01
        var neuro = aliceAffils.FirstOrDefault(a => a.InstitutionName == "Neurology Institute");
        Assert.IsNotNull(neuro);
        Assert.AreEqual(new DateOnly(2025, 3, 1), neuro.StartDate);
        Assert.IsFalse(neuro.IsPrimary);

        // Bob should have 1 affiliation
        var bob = await Context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.FullName == "Bob Jones");
        Assert.IsNotNull(bob);

        var bobAffils = await Context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == bob.Id)
            .ToListAsync();
        Assert.AreEqual(1, bobAffils.Count);
        Assert.AreEqual("Renal Associates", bobAffils[0].InstitutionName);
        Assert.IsTrue(bobAffils[0].IsPrimary, "Single affiliation should be primary");
    }

    [TestMethod]
    public async Task GetRejectedEntitiesPagedAsync_ReturnsFilteredByType()
    {
        Context.RejectedEntities.AddRange(
            new RejectedEntityEntity { EntityType = "keyword", Value = "bad-keyword", StudyNctId = "NCT001", RejectedAt = DateTime.UtcNow },
            new RejectedEntityEntity { EntityType = "keyword", Value = "noisy-term", StudyNctId = "NCT002", RejectedAt = DateTime.UtcNow },
            new RejectedEntityEntity { EntityType = "investigator_name", Value = "Pharma Inc", StudyNctId = "NCT003", RejectedAt = DateTime.UtcNow }
        );
        await Context.SaveChangesAsync();

        var (keywords, keywordTotal) = await _repo.GetRejectedEntitiesPagedAsync("keyword", 1, 10);
        Assert.AreEqual(2, keywordTotal);
        Assert.AreEqual(2, keywords.Count);
        Assert.IsTrue(keywords.All(k => k.EntityType == "keyword"));

        var (names, nameTotal) = await _repo.GetRejectedEntitiesPagedAsync("investigator_name", 1, 10);
        Assert.AreEqual(1, nameTotal);
        Assert.AreEqual(1, names.Count);
        Assert.AreEqual("Pharma Inc", names[0].Value);
    }

    [TestMethod]
    public async Task GetRejectedEntitiesPagedAsync_PaginationWorks()
    {
        for (int i = 1; i <= 5; i++)
        {
            Context.RejectedEntities.Add(new RejectedEntityEntity
            {
                EntityType = "keyword",
                Value = $"keyword-{i}",
                StudyNctId = $"NCT{i:D3}",
                RejectedAt = DateTime.UtcNow.AddDays(-i)
            });
        }
        await Context.SaveChangesAsync();

        var (page1, total) = await _repo.GetRejectedEntitiesPagedAsync("keyword", 1, 2);
        Assert.AreEqual(5, total);
        Assert.AreEqual(2, page1.Count);

        var (page3, _) = await _repo.GetRejectedEntitiesPagedAsync("keyword", 3, 2);
        Assert.AreEqual(1, page3.Count);
        Assert.AreEqual("keyword-5", page3[0].Value);
    }

    [TestMethod]
    public async Task GetNpiEnrichmentBreakdownAsync_ReturnsCorrectCounts()
    {
        var persons = new[]
        {
            new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "A", IsHuman = true, NpiEnrichmentResult = "assigned", NpiLookupAttemptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "B", IsHuman = true, NpiEnrichmentResult = "ambiguous", NpiLookupAttemptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "C", IsHuman = true, NpiEnrichmentResult = "not_found", NpiLookupAttemptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "D", IsHuman = true, NpiEnrichmentResult = "error", NpiLookupAttemptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "E", IsHuman = true, NpiEnrichmentResult = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "F", IsHuman = false, NpiEnrichmentResult = "assigned", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };
        Context.InvestigatorPersons.AddRange(persons);
        await Context.SaveChangesAsync();

        var breakdown = await _repo.GetNpiEnrichmentBreakdownAsync();

        Assert.AreEqual(1, breakdown["assigned"], "Only human + assigned");
        Assert.AreEqual(1, breakdown["ambiguous"]);
        Assert.AreEqual(1, breakdown["not_found"]);
        Assert.AreEqual(1, breakdown["error"]);
        Assert.AreEqual(1, breakdown["pending"]);
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status,
        Investigator[]? investigators, List<ClinicalTrialRecord.Reference>? references = null,
        DateOnly? startDate = null)
    {
        return new ClinicalTrialRecord
        {
            NctId = nctId,
            BriefTitle = title,
            OverallStatus = status,
            OverallOfficials = investigators?.ToList(),
            References = references,
            StartDate = startDate
        };
    }
}
