using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class StudyRepositoryHappyPathTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_PersistsStudiesInvestigatorsAndFilters()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000001", "Pregabalin for Neuropathic Pain Relief Trial", "RECRUITING",
                [
                    new Investigator { Name = "Alice Smith", Affiliation = "Acme Research", Role = "PRINCIPAL_INVESTIGATOR" },
                    new Investigator { Name = "Bob Jones", Affiliation = "Acme Research", Role = "SUB_INVESTIGATOR" }
                ]),
            CreateRecord("NCT00000002", "Aspirin Cardiovascular Prevention Study", "COMPLETED",
                [
                    new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" },
                    new Investigator { Name = "University of California", Affiliation = "Acme Pharma", Role = "SPONSOR" }
                ])
        ];

        var ingested = await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        Assert.AreEqual(records.Length, ingested);
        Assert.AreEqual(records.Length, await _repo.CountStudiesAsync());
        Assert.AreEqual(3, await _repo.CountInvestigatorsAsync());

        var byTitle = await _repo.GetStudiesPagedAsync(1, 10, search: "pregabalin");
        Assert.AreEqual(1, byTitle.Count);
        Assert.AreEqual("NCT00000001", byTitle[0].NctId);

        var byStatus = await _repo.GetStudiesPagedAsync(1, 10, status: "RECRUITING");
        Assert.AreEqual(1, byStatus.Count);
        Assert.AreEqual("NCT00000001", byStatus[0].NctId);

        var (rejected, total) = await _repo.GetRejectedEntitiesPagedAsync("investigator_name", 1, 50);
        Assert.AreEqual(1, total);
        Assert.AreEqual("University of California", rejected[0].Value);
        Assert.AreEqual("NCT00000002", rejected[0].StudyNctId);
        Assert.AreEqual("SPONSOR", rejected[0].Role);
        Assert.AreEqual("Acme Pharma", rejected[0].Affiliation);
        Assert.AreEqual("OrgKeywords:UNIVERSITY", rejected[0].RejectionReason);

        var (filtered, filteredTotal) = await _repo.GetRejectedEntitiesPagedAsync(
            "investigator_name", 1, 50, reason: "OrgKeywords:UNIVERSITY", nctId: "NCT00000002");
        Assert.AreEqual(1, filteredTotal);
        Assert.AreEqual("University of California", filtered[0].Value);

        var (empty, emptyTotal) = await _repo.GetRejectedEntitiesPagedAsync(
            "investigator_name", 1, 50, role: "PRINCIPAL_INVESTIGATOR");
        Assert.AreEqual(0, emptyTotal);
        Assert.AreEqual(0, empty.Count);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_IsIdempotent()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            [
                new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" },
                new Investigator { Name = "Pfizer", Affiliation = "Pharma HQ", Role = "SPONSOR" }
            ]);
        record.Keywords = ["Pilot Study"];
        record.Phases = ["PHASE1"];
        record.Locations =
        [
            new StudyListResponse.Location
            {
                Facility = "Research Center",
                City = "Boston",
                State = "MA",
                Country = "United States"
            }
        ];
        record.References =
        [
            new ClinicalTrialRecord.Reference { Pmid = "12345678", Citation = "Study citation", Type = "background" }
        ];
        record.PrimaryOutcomes =
        [
            new ClinicalTrialRecord.Outcome { Measure = "Primary measure", Description = "Primary description", TimeFrame = "12 weeks" }
        ];
        record.SecondaryOutcomes =
        [
            new ClinicalTrialRecord.Outcome { Measure = "Secondary measure", Description = "Secondary description", TimeFrame = "24 weeks" }
        ];
        record.ArmGroups =
        [
            new ClinicalTrialRecord.ArmGroup { Label = "Treatment", Type = "Experimental", Description = "Treatment arm" }
        ];
        record.Interventions =
        [
            new ClinicalTrialRecord.Intervention { Name = "Study intervention", Type = "Drug", Description = "Intervention description" }
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);
        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        Assert.AreEqual(1, await _repo.CountStudiesAsync());
        Assert.AreEqual(1, await _repo.CountInvestigatorsAsync());
        Assert.AreEqual(1, await Context.StudyKeywords.AsNoTracking().CountAsync(k => k.StudyNctId == record.NctId));
        Assert.AreEqual(1, await Context.StudyPhases.AsNoTracking().CountAsync(p => p.StudyNctId == record.NctId));
        Assert.AreEqual(1, await Context.StudyLocations.AsNoTracking().CountAsync(l => l.StudyNctId == record.NctId));
        Assert.AreEqual(1, await Context.StudyReferences.AsNoTracking().CountAsync(r => r.StudyNctId == record.NctId));
        Assert.AreEqual(2, await Context.StudyOutcomes.AsNoTracking().CountAsync(o => o.StudyNctId == record.NctId));
        Assert.AreEqual(1, await Context.StudyArmGroups.AsNoTracking().CountAsync(a => a.StudyNctId == record.NctId));
        Assert.AreEqual(1, await Context.StudyInterventions.AsNoTracking().CountAsync(i => i.StudyNctId == record.NctId));

        await _repo.UpdateStudiesWithClinicalTrialsAsync(
        [
            new ClinicalTrialRecord { NctId = record.NctId }
        ]);

        var preservedStudy = await Context.Studies.AsNoTracking().SingleAsync(s => s.NctId == record.NctId);
        Assert.AreEqual("Study Three", preservedStudy.BriefTitle);
        Assert.AreEqual("ACTIVE", preservedStudy.OverallStatus);
        Assert.AreEqual(1, await Context.StudyKeywords.AsNoTracking().CountAsync(k => k.StudyNctId == record.NctId));
        Assert.AreEqual(2, await Context.StudyOutcomes.AsNoTracking().CountAsync(o => o.StudyNctId == record.NctId));
        Assert.AreEqual(1, await Context.StudyInterventions.AsNoTracking().CountAsync(i => i.StudyNctId == record.NctId));

        var emptyFields = new ClinicalTrialRecord
        {
            NctId = record.NctId,
            BriefTitle = "Study Three Updated",
            OverallStatus = "COMPLETED",
            OverallOfficials = record.OverallOfficials,
            Keywords = [],
            Phases = [],
            Locations = [],
            References = [],
            PrimaryOutcomes = [],
            SecondaryOutcomes = [],
            ArmGroups = [],
            Interventions = []
        };
        await _repo.UpdateStudiesWithClinicalTrialsAsync([emptyFields]);

        var updatedStudy = await Context.Studies.AsNoTracking().SingleAsync(s => s.NctId == record.NctId);
        Assert.AreEqual("Study Three Updated", updatedStudy.BriefTitle);
        Assert.AreEqual("COMPLETED", updatedStudy.OverallStatus);
        Assert.AreEqual(0, await Context.StudyKeywords.AsNoTracking().CountAsync(k => k.StudyNctId == record.NctId));
        Assert.AreEqual(0, await Context.StudyPhases.AsNoTracking().CountAsync(p => p.StudyNctId == record.NctId));
        Assert.AreEqual(0, await Context.StudyLocations.AsNoTracking().CountAsync(l => l.StudyNctId == record.NctId));
        Assert.AreEqual(0, await Context.StudyReferences.AsNoTracking().CountAsync(r => r.StudyNctId == record.NctId));
        Assert.AreEqual(0, await Context.StudyOutcomes.AsNoTracking().CountAsync(o => o.StudyNctId == record.NctId));
        Assert.AreEqual(0, await Context.StudyArmGroups.AsNoTracking().CountAsync(a => a.StudyNctId == record.NctId));
        Assert.AreEqual(0, await Context.StudyInterventions.AsNoTracking().CountAsync(i => i.StudyNctId == record.NctId));

        var (rejected, _) = await _repo.GetRejectedEntitiesPagedAsync("investigator_name", 1, 50);
        Assert.IsTrue(rejected.Count == 3, "Each complete ingest run records the rejected sponsor");
        Assert.IsTrue(rejected.All(r => r.Value == "Pfizer"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_OverriddenRejectedNameIsIngestedAsHuman()
    {
        ClinicalTrialRecord record = CreateRecord("NCT00000003", "Study Three", "ACTIVE",
            [
                new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" },
                new Investigator { Name = "Pfizer", Affiliation = "Pharma HQ", Role = "SPONSOR" }
            ]);

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var (rejected, _) = await _repo.GetRejectedEntitiesPagedAsync("investigator_name", 1, 50);
        Assert.AreEqual(1, rejected.Count);
        Assert.AreEqual("Pfizer", rejected[0].Value);

        var rejectedName = new RejectedInvestigatorNameEntity
        {
            FullName = "Pfizer",
            OccurrenceCount = 1,
            StudyCount = 1,
            RejectionReason = "PharmaBlocklist:PFIZER",
            IsHumanOverride = true,
            Note = "verified via NPPES"
        };
        Context.RejectedInvestigatorNames.Add(rejectedName);
        await Context.SaveChangesAsync();

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var personsAfter = await Context.InvestigatorPersons
            .Where(p => p.FullName == "Pfizer")
            .ToListAsync();
        Assert.AreEqual(1, personsAfter.Count, "Overridden name must be ingested as a person on the next run");
        Assert.IsTrue(personsAfter[0].IsHuman);

        var (rejectedAfter, _) = await _repo.GetRejectedEntitiesPagedAsync("investigator_name", 1, 50);
        Assert.AreEqual(1, rejectedAfter.Count, "Overridden name must not be rejected again");
        Assert.AreEqual(1, rejectedAfter.Count(r => r.Value == "Pfizer"),
            "Only the pre-override rejection row remains — no new rejection for an overridden name");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SetRejectedInvestigatorNameOverrideAsync_UpdatesFlags()
    {
        var rejectedName = new RejectedInvestigatorNameEntity
        {
            FullName = "Acme Corp",
            OccurrenceCount = 5,
            StudyCount = 3,
            RejectionReason = "CorporateSuffix:CORP"
        };
        Context.RejectedInvestigatorNames.Add(rejectedName);
        await Context.SaveChangesAsync();

        var updated = await _repo.SetRejectedInvestigatorNameOverrideAsync(rejectedName.Id, false, "confirmed as organization");
        Assert.IsTrue(updated);

        var after = await Context.RejectedInvestigatorNames
            .AsNoTracking()
            .FirstAsync(n => n.Id == rejectedName.Id);
        Assert.AreEqual(false, after.IsHumanOverride);
        Assert.AreEqual("confirmed as organization", after.Note);

        var missing = await _repo.SetRejectedInvestigatorNameOverrideAsync(Guid.NewGuid(), true, null);
        Assert.IsFalse(missing);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RequeueAmbiguousNpiLookups_ResetsAndEnqueues()
    {
        var ambiguousPerson = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Ambiguous Person",
            IsHuman = true,
            NpiEnrichmentResult = "ambiguous",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var assignedPerson = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Assigned Person",
            IsHuman = true,
            Npi = "1234567890",
            NpiEnrichmentResult = "assigned",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var notFoundPerson = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. NotFound Person",
            IsHuman = true,
            NpiEnrichmentResult = "not_found",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.AddRange(ambiguousPerson, assignedPerson, notFoundPerson);
        Context.PersonIdentifierCandidates.Add(new PersonIdentifierCandidateEntity
        {
            PersonId = ambiguousPerson.Id,
            IdentifierType = "NPI",
            IdentifierValue = "1111111111",
            SourceName = "NPPES",
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var requeued = await _repo.RequeueAmbiguousNpiLookupsAsync();

        Assert.AreEqual(1, requeued);

        var ambiguousAfter = await Context.InvestigatorPersons.AsNoTracking().FirstAsync(p => p.Id == ambiguousPerson.Id);
        Assert.IsNull(ambiguousAfter.NpiLookupAttemptedAt);
        Assert.IsNull(ambiguousAfter.NpiEnrichmentResult);

        var assignedAfter = await Context.InvestigatorPersons.AsNoTracking().FirstAsync(p => p.Id == assignedPerson.Id);
        Assert.AreEqual("assigned", assignedAfter.NpiEnrichmentResult);
        Assert.IsNotNull(assignedAfter.NpiLookupAttemptedAt);

        var notFoundAfter = await Context.InvestigatorPersons.AsNoTracking().FirstAsync(p => p.Id == notFoundPerson.Id);
        Assert.AreEqual("not_found", notFoundAfter.NpiEnrichmentResult);

        Assert.AreEqual(0, await Context.PersonIdentifierCandidates.CountAsync(c => c.PersonId == ambiguousPerson.Id));
        Assert.AreEqual(1, await Context.PipelineEvents.CountAsync(e => e.EventType == "investigator.enrichment"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RequeueAmbiguousNpiLookups_AlreadyPendingEvent_DoesNotDuplicate()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Pending Person",
            IsHuman = true,
            NpiEnrichmentResult = "ambiguous",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        Context.PipelineEvents.Add(new PipelineEventEntity
        {
            EventType = "investigator.enrichment",
            Data = person.Id.ToString(),
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var requeued = await _repo.RequeueAmbiguousNpiLookupsAsync();

        Assert.AreEqual(0, requeued, "No duplicate event enqueued for a person with a pending event");
        var personAfter = await Context.InvestigatorPersons.AsNoTracking().FirstAsync(p => p.Id == person.Id);
        Assert.IsNull(personAfter.NpiLookupAttemptedAt,
            "Person still reset so the pending event re-runs the enrichment");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_NormalizesLocationFreeText()
    {
        // Issue #380: country aliases -> canonical, US states -> 2-letter codes,
        // city/facility trimmed and whitespace-collapsed.
        ClinicalTrialRecord record = CreateRecord("NCT00000006", "Location Normalization Study", "RECRUITING",
            [
                new Investigator { Name = "George Hale", Affiliation = "Med Center", Role = "PRINCIPAL_INVESTIGATOR" }
            ]);
        record.Locations =
        [
            new StudyListResponse.Location
            {
                Facility = "  Johns  Hopkins   Hospital ",
                City = "  Baltimore  ",
                State = "Maryland",
                Country = "U.S.A."
            },
            new StudyListResponse.Location
            {
                Facility = "Berlin  Clinic",
                City = " Berlin ",
                State = " Berlin ",
                Country = " Germany "
            }
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var stored = await Context.StudyLocations
            .Where(l => l.StudyNctId == "NCT00000006")
            .OrderBy(l => l.Facility)
            .ToListAsync();
        Assert.AreEqual(2, stored.Count);

        var us = stored.First(l => l.Country == "United States");
        Assert.AreEqual("Johns Hopkins Hospital", us.Facility);
        Assert.AreEqual("Baltimore", us.City);
        Assert.AreEqual("MD", us.State, "US state full name must become its 2-letter code");

        var de = stored.First(l => l.Country == "Germany");
        Assert.AreEqual("Berlin Clinic", de.Facility);
        Assert.AreEqual("Berlin", de.City);
        Assert.AreEqual("Berlin", de.State, "Non-US state/province preserved as-is");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_ExpandsAcronyms_PersistsFullTermsAndRecordsEvaluations()
    {
        // Issue #355: whole-keyword acronyms expand to canonical medical terms
        // BEFORE the length/blocklist rules, so MI/CVA/DKA/PE are persisted.
        using var matcher = new MeSHMatcher(Path.Combine(AppContext.BaseDirectory, "Resources", "mesh"));
        var repo = new StudyRepository(ConnectionString, meshMatcher: matcher);

        ClinicalTrialRecord record = CreateRecord("NCT00000005", "Acronym Keyword Study", "RECRUITING",
            [
                new Investigator { Name = "Fiona Grant", Affiliation = "Med Center", Role = "PRINCIPAL_INVESTIGATOR" }
            ]);
        record.Keywords = ["MI", "CVA", "DKA", "PE", "XZ"];

        var ingested = await repo.UpdateStudiesWithClinicalTrialsAsync([record]);
        Assert.AreEqual(1, ingested);

        var storedKeywords = await Context.StudyKeywords
            .Where(k => k.StudyNctId == "NCT00000005")
            .Select(k => k.Keyword)
            .OrderBy(k => k)
            .ToListAsync();
        CollectionAssert.AreEquivalent(
            new[] { "CEREBROVASCULAR ACCIDENT", "DIABETIC KETOACIDOSIS", "MYOCARDIAL INFARCTION", "PULMONARY EMBOLISM" },
            storedKeywords,
            "Acronyms must be persisted as their expanded canonical terms; unknown acronym XZ stays rejected");

        var evaluations = await Context.RejectedTerms
            .Where(t => t.StudyNctId == "NCT00000005" && t.Source == "keyword")
            .ToListAsync();
        Assert.AreEqual(9, evaluations.Count, "5 raw + 4 expanded keyword evaluations recorded (XZ has no expansion)");
        CollectionAssert.Contains(
            evaluations.Select(e => e.Value).ToList(),
            "myocardial infarction",
            "Expanded form must be recorded for analysis");
        var expandedMatch = evaluations.First(e => e.Value == "myocardial infarction");
        Assert.IsTrue(expandedMatch.SideBMatched, "Expanded term resolves to its MeSH descriptor (>= 0.65)");
        Assert.AreEqual("Myocardial Infarction", expandedMatch.SideBMeshTerm);
        var rawMi = evaluations.First(e => e.Value == "MI");
        Assert.IsTrue(rawMi.SideBMatched,
            "Raw 'MI' scores 0.71 with the BioBERT model — above the 0.65 re-picked threshold, so the gate rescues it even unexpanded");
        var rawCva = evaluations.First(e => e.Value == "CVA");
        Assert.IsFalse(rawCva.SideBMatched,
            "Raw acronym 'CVA' (0.54) scores below the threshold — expansion is what rescues it");
        Assert.AreEqual(5, evaluations.Count(e => e.SideBMatched), "4 expanded canonical terms + raw MI match MeSH");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ScrubInvestigatorPapersAsync_OnlyScopesToPersonsOwnStudies()
    {
        // Regression test for issue #334: the scrub previously scanned every distinct
        // PMID in the database per person. It must only link the person to papers
        // cited by their own studies.
        var personId = Guid.NewGuid();
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity
        {
            Id = personId,
            FullName = "Eve Principal",
            IsHuman = true,
            // ORCID set so the scrub's author-matching path performs no PubMed fetch.
            Orcid = "0000-0001-2345-6789",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.Studies.Add(new StudyEntity { NctId = "NCT500001", BriefTitle = "Own Study", OverallStatus = "ACTIVE", CreatedAt = DateTime.UtcNow });
        Context.Studies.Add(new StudyEntity { NctId = "NCT500002", BriefTitle = "Other Study", OverallStatus = "ACTIVE", CreatedAt = DateTime.UtcNow });
        Context.StudyInvestigators.Add(new StudyInvestigatorEntity { StudyNctId = "NCT500001", InvestigatorPersonId = personId, IsOverallOfficial = true });
        Context.StudyReferences.AddRange(
            new StudyReferenceEntity { StudyNctId = "NCT500001", Pmid = "30001001", Citation = "Paper A" },
            new StudyReferenceEntity { StudyNctId = "NCT500001", Pmid = "30001002", Citation = "Paper B" },
            new StudyReferenceEntity { StudyNctId = "NCT500002", Pmid = "30001003", Citation = "Paper C" });
        foreach (var pmid in new[] { "30001001", "30001002", "30001003" })
        {
            Context.PubmedPapers.Add(new PubmedPaperEntity
            {
                Id = Guid.NewGuid(),
                Pmid = pmid,
                Title = $"Paper {pmid}",
                Journal = "Journal",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await Context.SaveChangesAsync();

        await StudyRepository.ScrubInvestigatorPapersAsync(Context, personId, CancellationToken.None);

        var linkedPmids = await Context.InvestigatorPapers
            .Where(ip => ip.InvestigatorPersonId == personId)
            .Select(ip => ip.PubmedPaper!.Pmid)
            .ToListAsync();
        CollectionAssert.AreEquivalent(new[] { "30001001", "30001002" }, linkedPmids);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_AcceptsDesignDescriptors_RejectsOnlyJunk()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000009",
            BriefTitle = "Design Descriptor Keywords Study",
            OverallStatus = "RECRUITING",
            Conditions = ["Diabetes"],
            OverallOfficials =
            [
                new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "PRINCIPAL_INVESTIGATOR" }
            ],
            Keywords = ["Pilot Study", "Randomised Controlled Trial", "safety", "treatment", "Diabetes"]
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var keywords = await Context.StudyKeywords
            .Where(k => k.StudyNctId == "NCT00000009")
            .Select(k => k.Keyword)
            .ToListAsync();
        CollectionAssert.Contains(keywords, "PILOT STUDY");
        CollectionAssert.Contains(keywords, "RANDOMISED CONTROLLED TRIAL");
        Assert.AreEqual(2, keywords.Count, "Condition-duplicate DIABETES is stored as a condition, not a keyword");
        Assert.IsFalse(keywords.Contains("SAFETY"), "Junk keyword must not be persisted");
        Assert.IsFalse(keywords.Contains("TREATMENT"), "Junk keyword must not be persisted");

        var (rejected, total) = await _repo.GetRejectedEntitiesPagedAsync("keyword", 1, 50);
        Assert.AreEqual(2, total, "Only SAFETY and TREATMENT are rejected");
        CollectionAssert.AreEquivalent(
            new[] { "SAFETY", "TREATMENT" },
            rejected.Select(r => r.Value).ToList());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpsertStudiesAsync_MeSHGate_RescuesNothingJunk()
    {
        var meshResourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");
        using var matcher = new Scrapers.Services.MeSHMatcher(meshResourcesPath);
        var repo = new StudyRepository(ConnectionString, matcher);

        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00000010",
            BriefTitle = "MeSH Gate Keywords Study",
            OverallStatus = "RECRUITING",
            OverallOfficials =
            [
                new Investigator { Name = "Eve Adams", Affiliation = "Wellness Org", Role = "PRINCIPAL_INVESTIGATOR" }
            ],
            Keywords = ["treatment", "prognosis", "CVA", "Pilot Study"]
        };

        await repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        var keywords = await Context.StudyKeywords
            .Where(k => k.StudyNctId == "NCT00000010")
            .Select(k => k.Keyword)
            .ToListAsync();
        Assert.AreEqual(2, keywords.Count,
            "Allowlisted design descriptor + acronym-expanded CVA are kept (issue #355: CVA -> cerebrovascular accident)");
        CollectionAssert.Contains(keywords, "PILOT STUDY");
        CollectionAssert.Contains(keywords, "CEREBROVASCULAR ACCIDENT");

        var (rejected, total) = await repo.GetRejectedEntitiesPagedAsync("keyword", 1, 50);
        Assert.AreEqual(2, total, "Junk stays rejected even when it matches MeSH");
        CollectionAssert.AreEquivalent(
            new[] { "TREATMENT", "PROGNOSIS" },
            rejected.Select(r => r.Value).ToList());
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
