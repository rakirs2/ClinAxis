using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.IntegrationTests.Utilities;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class PubMedScraperDbIntegrationTests : EphemeralDbTestBase
    {
        [TestMethod]
        public async Task IngestPubMedPapersAsync_SkipsIncompleteStudies()
        {
            var study = new StudyEntity
            {
                NctId = "NCT00000001",
                BriefTitle = "Incomplete Study",
                OverallStatus = "Recruiting",
                IsIncomplete = true
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var scraper = new PubMedScraperService(ConnectionString);
            var count = await scraper.IngestPubMedPapersAsync();

            Assert.AreEqual(0, count, "No papers should be ingested for incomplete studies.");
        }

        [TestMethod]
        public async Task IngestPubMedPapersAsync_DoesNotDuplicatePmids()
        {
            var study = new StudyEntity
            {
                NctId = "NCT00000002",
                BriefTitle = "Dedup Study",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var existingPaper = new PubmedStudyEntity
            {
                StudyNctId = study.NctId,
                Pmid = "12345678",
                Title = "Existing Paper",
                CreatedAt = DateTime.UtcNow
            };
            Context.PubmedStudies.Add(existingPaper);
            await Context.SaveChangesAsync();

            var scraper = new PubMedScraperService(ConnectionString);
            var count = await scraper.IngestPubMedPapersAsync();

            var papers = await Context.PubmedStudies
                .Where(p => p.StudyNctId == study.NctId)
                .ToListAsync();
            Assert.AreEqual(1, papers.Count);
            Assert.AreEqual("Existing Paper", papers[0].Title);
        }
    }
}