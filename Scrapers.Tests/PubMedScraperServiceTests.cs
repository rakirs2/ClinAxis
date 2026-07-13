using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Testing;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.Tests
{
    [TestClass]
    public class PubMedScraperServiceTests : DbTestBase
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

            var existingPaper = new PubmedPaperEntity
            {
                Pmid = "12345678",
                Title = "Existing Paper Title",
            };
            Context.PubmedPapers.Add(existingPaper);
            await Context.SaveChangesAsync();

            // Link the existing paper to the study
            Context.StudyPapers.Add(new StudyPaperEntity
            {
                StudyNctId = study.NctId,
                PubmedPaperId = existingPaper.Id,
            });
            await Context.SaveChangesAsync();

            var scraper = new PubMedScraperService(ConnectionString);
            var count = await scraper.IngestPubMedPapersAsync();

            var linkCount = await Context.StudyPapers.CountAsync(sp => sp.StudyNctId == study.NctId);
            Assert.AreEqual(1, linkCount);
            var paper = await Context.PubmedPapers.FirstAsync(p => p.Pmid == "12345678");
            Assert.AreEqual("Existing Paper Title", paper.Title);
        }
    }
}
