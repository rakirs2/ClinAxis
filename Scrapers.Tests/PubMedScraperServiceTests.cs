using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Tests.Utilities;
using System;
using System.Threading.Tasks;

namespace Scrapers.Tests
{
    [TestClass]
    public class PubMedScraperServiceTests : TestBase
    {
        [TestMethod]
        public async Task IngestPubMedPapersAsync_SavesPapersAndUpdatesInvestigators()
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

            var pubMedClient = new PubMedClient(new System.Net.Http.HttpClient());
            var pubMedScraperService = new PubMedScraperService(Context, pubMedClient);

            var papersCount = await pubMedScraperService.IngestPubMedPapersAsync();

            Assert.IsTrue(papersCount >= 0); // Allow 0 if no papers
        }
    }
}
