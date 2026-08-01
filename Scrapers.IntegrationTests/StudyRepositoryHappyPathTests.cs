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
                [new Investigator { Name = "Carol White", Affiliation = "Health Org", Role = "STUDY_DIRECTOR" }])
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
    public async Task StatsCounts_MatchDatabaseState()
    {
        var paperId = Guid.NewGuid();
        Context.PubmedPapers.Add(new PubmedPaperEntity { Id = paperId, Pmid = "30000001", Title = "Linked Paper", Journal = "Journal", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        Context.Studies.Add(new StudyEntity { NctId = "NCT400001", BriefTitle = "Main Study", OverallStatus = "ACTIVE", CreatedAt = DateTime.UtcNow });
        Context.InvestigatorPersons.Add(new InvestigatorPersonEntity { Id = Guid.NewGuid(), FullName = "Dr. Person", IsHuman = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        Context.StudyPapers.Add(new StudyPaperEntity { StudyNctId = "NCT400001", PubmedPaperId = paperId });
        Context.StudyKeywords.Add(new StudyKeywordEntity { StudyNctId = "NCT400001", Keyword = "ONCOLOGY" });
        await Context.SaveChangesAsync();

        Assert.AreEqual(1, await _repo.CountStudiesAsync());
        Assert.AreEqual(1, await _repo.CountInvestigatorsAsync());
        Assert.AreEqual(1, await _repo.CountPubmedPapersAsync());
        Assert.AreEqual(1, await _repo.CountKeywordsAsync());
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
