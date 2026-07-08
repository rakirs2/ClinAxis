using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.IntegrationTests.Fakes;
using Scrapers.IntegrationTests.Utilities;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class PubMedScraperDbIntegrationTests : EphemeralDbTestBase
    {
        [TestMethod]
        public async Task IngestPubMedPapersAsync_PersistsMultiplePapersForOneInvestigator()
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
                Name = "Dr. Researcher",
                StudyId = study.Id
            };
            Context.Investigators.Add(investigator);
            await Context.SaveChangesAsync();

            var fakePapers = new List<PubMedPaper>
            {
                new PubMedPaper("Clinical trial of cancer drug therapy", "https://pubmed.ncbi.nlm.nih.gov/11111/", "ncbi1", "orcid1"),
                new PubMedPaper("Therapy effects on cancerous cells", "https://pubmed.ncbi.nlm.nih.gov/22222/"),
                new PubMedPaper("Oncology clinical outcomes study", "https://pubmed.ncbi.nlm.nih.gov/33333/", "ncbi3")
            };

            var fakeClient = new FakePubMedClient(fakePapers);
            var scraper = new PubMedScraperService(Context, fakeClient);

            var count = await scraper.IngestPubMedPapersAsync();

            Assert.AreEqual(3, count, "All three papers should be ingested.");

            var papers = await Context.PubmedStudies
                .Where(p => p.InvestigatorId == investigator.Id)
                .OrderBy(p => p.Title)
                .ToListAsync();

            Assert.AreEqual(3, papers.Count);
            Assert.IsTrue(papers.Any(p => p.Url == "https://pubmed.ncbi.nlm.nih.gov/11111/"));
            Assert.IsTrue(papers.Any(p => p.Url == "https://pubmed.ncbi.nlm.nih.gov/22222/"));
            Assert.IsTrue(papers.Any(p => p.Url == "https://pubmed.ncbi.nlm.nih.gov/33333/"));
        }

        [TestMethod]
        public async Task IngestPubMedPapersAsync_SkipsIncompleteStudies()
        {
            var study = new StudyEntity
            {
                NctId = Guid.NewGuid().ToString(),
                BriefTitle = "Incomplete Study",
                OverallStatus = "Recruiting",
                IsIncomplete = true
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var investigator = new InvestigatorEntity
            {
                Name = "Incomplete Researcher",
                StudyId = study.Id
            };
            Context.Investigators.Add(investigator);
            await Context.SaveChangesAsync();

            var fakeClient = new FakePubMedClient(
                new List<PubMedPaper> { new PubMedPaper("Should not appear", "https://pubmed.ncbi.nlm.nih.gov/99999/") });
            var scraper = new PubMedScraperService(Context, fakeClient);

            var count = await scraper.IngestPubMedPapersAsync();

            Assert.AreEqual(0, count, "No papers should be ingested for incomplete studies.");

            var papers = await Context.PubmedStudies
                .Where(p => p.InvestigatorId == investigator.Id)
                .ToListAsync();
            Assert.AreEqual(0, papers.Count);
        }

        [TestMethod]
        public async Task IngestPubMedPapersAsync_UpdatesLastSuccessfulPubmedCrawl()
        {
            var study = new StudyEntity
            {
                NctId = Guid.NewGuid().ToString(),
                BriefTitle = "Crawl Test Study",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var investigator = new InvestigatorEntity
            {
                Name = "Crawl Tester",
                StudyId = study.Id,
                LastSuccessfulPubmedCrawl = null
            };
            Context.Investigators.Add(investigator);
            await Context.SaveChangesAsync();

            var fakeClient = new FakePubMedClient(
                new List<PubMedPaper> { new PubMedPaper("Crawl paper", "https://pubmed.ncbi.nlm.nih.gov/44444/") });
            var scraper = new PubMedScraperService(Context, fakeClient);

            await scraper.IngestPubMedPapersAsync();

            var updatedInvestigator = await Context.Investigators.FirstAsync(i => i.Id == investigator.Id);
            Assert.IsNotNull(updatedInvestigator.LastSuccessfulPubmedCrawl);
            Assert.IsTrue(updatedInvestigator.LastSuccessfulPubmedCrawl.Value > DateTime.UtcNow.AddMinutes(-1));
        }

        [TestMethod]
        public async Task IngestPubMedPapersAsync_DoesNotDuplicateTitles()
        {
            var study = new StudyEntity
            {
                NctId = Guid.NewGuid().ToString(),
                BriefTitle = "Dedup Study",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var investigator = new InvestigatorEntity
            {
                Name = "Dedup Tester",
                StudyId = study.Id
            };
            Context.Investigators.Add(investigator);
            await Context.SaveChangesAsync();

            var existingPaper = new PubmedStudyEntity
            {
                InvestigatorId = investigator.Id,
                Title = "Existing Paper Title",
                Url = "https://pubmed.ncbi.nlm.nih.gov/old/",
                Keywords = "clinical",
                CreatedAt = DateTime.UtcNow
            };
            Context.PubmedStudies.Add(existingPaper);
            await Context.SaveChangesAsync();

            var fakePapers = new List<PubMedPaper>
            {
                new PubMedPaper("Existing Paper Title", "https://pubmed.ncbi.nlm.nih.gov/old/"),
                new PubMedPaper("New Paper Title", "https://pubmed.ncbi.nlm.nih.gov/new/")
            };
            var fakeClient = new FakePubMedClient(fakePapers);
            var scraper = new PubMedScraperService(Context, fakeClient);

            var count = await scraper.IngestPubMedPapersAsync();

            Assert.AreEqual(2, count, "Returned count is total papers from client including duplicates.");

            var papers = await Context.PubmedStudies
                .Where(p => p.InvestigatorId == investigator.Id)
                .ToListAsync();
            Assert.AreEqual(2, papers.Count);
            Assert.IsTrue(papers.Any(p => p.Title == "Existing Paper Title"));
            Assert.IsTrue(papers.Any(p => p.Title == "New Paper Title"));
        }

        [TestMethod]
        public async Task IngestPubMedPapersAsync_ExtractsKeywords()
        {
            var study = new StudyEntity
            {
                NctId = Guid.NewGuid().ToString(),
                BriefTitle = "Keyword Test",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);
            await Context.SaveChangesAsync();

            var investigator = new InvestigatorEntity
            {
                Name = "Keyword Tester",
                StudyId = study.Id
            };
            Context.Investigators.Add(investigator);
            await Context.SaveChangesAsync();

            var fakePapers = new List<PubMedPaper>
            {
                new PubMedPaper("Clinical trial of cancer drug therapy", "https://pubmed.ncbi.nlm.nih.gov/keywords/")
            };
            var fakeClient = new FakePubMedClient(fakePapers);
            var scraper = new PubMedScraperService(Context, fakeClient);

            await scraper.IngestPubMedPapersAsync();

            var paper = await Context.PubmedStudies
                .FirstAsync(p => p.InvestigatorId == investigator.Id);

            var keywords = paper.Keywords?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Assert.IsNotNull(keywords);
            Assert.IsTrue(keywords.Contains("clinical"));
            Assert.IsTrue(keywords.Contains("trial"));
            Assert.IsTrue(keywords.Contains("cancer"));
            Assert.IsTrue(keywords.Contains("drug"));
            Assert.IsTrue(keywords.Contains("therapy"));
            Assert.AreEqual(5, keywords.Count);
        }
    }
}
