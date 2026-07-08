using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Tests.Utilities;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.Tests
{
    [TestClass]
    public class PubMedScraperServiceTests : EphemeralDbTestBase
    {
        [TestMethod]
        public async Task IngestPubMedPapersAsync_DoesNotInsertDuplicatePmids()
        {
            var study = new StudyEntity
            {
                NctId = "NCT00000001",
                BriefTitle = "Test Study",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var existingPaper = new PubmedStudyEntity
            {
                StudyNctId = study.NctId,
                Pmid = "12345678",
                Title = "Existing Paper Title",
                CreatedAt = DateTime.UtcNow
            };
            Context.PubmedStudies.Add(existingPaper);
            await Context.SaveChangesAsync();

            var scraper = new PubMedScraperService(ConnectionString);
            var count = await scraper.IngestPubMedPapersAsync();

            List<PubmedStudyEntity> papers = await Context.PubmedStudies.Where(p => p.StudyNctId == study.NctId).ToListAsync();
            Assert.AreEqual(1, papers.Count);
            Assert.AreEqual("Existing Paper Title", papers[0].Title);
        }
    }
}
