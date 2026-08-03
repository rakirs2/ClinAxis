using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
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
            [new Investigator { Name = "Dana King", Affiliation = "Wellness Org", Role = "STUDY_DIRECTOR" }]);

        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);
        await _repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        Assert.AreEqual(1, await _repo.CountStudiesAsync());
        Assert.AreEqual(1, await _repo.CountInvestigatorsAsync());
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
