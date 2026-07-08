using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Tests.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.Tests
{
    [TestClass]
    public class PubMedScraperServiceTests : EphemeralDbTestBase
    {
        [TestMethod]
        public async Task IngestPubMedPapersAsync_DoesNotInsertDuplicateTitles()
        {
            var study = new StudyEntity
            {
                NctId = Guid.NewGuid().ToString(),
                BriefTitle = "Test Study",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var investigator = new InvestigatorEntity
            {
                Name = "Test Investigator",
                StudyId = study.Id
            };
            Context.Investigators.Add(investigator);
            await Context.SaveChangesAsync();

            // Add an existing PubMed study
            var existingPaper = new PubmedStudyEntity
            {
                InvestigatorId = investigator.Id,
                Title = "Existing Paper Title",
                Url = "http://example.com",
                Keywords = "clinical",
                CreatedAt = DateTime.UtcNow
            };
            Context.PubmedStudies.Add(existingPaper);
            await Context.SaveChangesAsync();

            // Prepare fake PubMed client with duplicate paperTitle
            var fakeClient = new FakePubMedClientWithDuplicates("Existing Paper Title", "New Paper Title");

            var scraper = new PubMedScraperService(Context, fakeClient);
            var count = await scraper.IngestPubMedPapersAsync();

            // Only the new paper should be added
            var papers = await Context.PubmedStudies.Where(p => p.InvestigatorId == investigator.Id).ToListAsync();
            Assert.AreEqual(2, papers.Count);
            Assert.IsTrue(papers.Any(p => p.Title == "Existing Paper Title"));
            Assert.IsTrue(papers.Any(p => p.Title == "New Paper Title"));
        }
    }

    public class FakePubMedClientWithDuplicates : IPubMedClient
    {
        private readonly string _existingTitle;
        private readonly string _newTitle;

        public FakePubMedClientWithDuplicates(string existingTitle, string newTitle)
        {
            _existingTitle = existingTitle;
            _newTitle = newTitle;
        }

        public Task<List<PubMedPaper>> GetPapersForInvestigatorAsync(InvestigatorEntity investigator, CancellationToken cancellationToken)
        {
            var papers = new List<PubMedPaper>
            {
                new PubMedPaper(_existingTitle, "http://example.com/1"),
                new PubMedPaper(_newTitle, "http://example.com/2")
            };
            return Task.FromResult(papers);
        }
    }
}
