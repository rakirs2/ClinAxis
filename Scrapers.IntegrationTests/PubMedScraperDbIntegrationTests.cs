using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class PubMedScraperDbIntegrationTests : DbTestBase
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

            var existingPaper = new PubmedPaperEntity
            {
                Pmid = "12345678",
                Title = "Existing Paper",
                PublicationTypes = "Journal Article",
            };
            Context.PubmedPapers.Add(existingPaper);
            await Context.SaveChangesAsync();

            Context.StudyPapers.Add(new StudyPaperEntity
            {
                StudyNctId = study.NctId,
                PubmedPaperId = existingPaper.Id,
            });
            await Context.SaveChangesAsync();

            var scraper = new PubMedScraperService(ConnectionString);
            var count = await scraper.IngestPubMedPapersAsync();

            var papers = await Context.PubmedPapers
                .Where(p => p.Pmid == "12345678")
                .ToListAsync();
            Assert.AreEqual(1, papers.Count);
            Assert.AreEqual("Existing Paper", papers[0].Title);
        }
    }
}
