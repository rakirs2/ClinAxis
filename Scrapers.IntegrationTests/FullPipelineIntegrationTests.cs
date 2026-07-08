using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Services;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Linq;
using Scrapers.IntegrationTests.Utilities;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class FullPipelineIntegrationTests : TestBase
    {
        [TestMethod]
        public async Task FullScraperPipeline_RunsAndVerifiesDataAsync()
        {
            var clinicalTrialsClient = new ClinicalTrialsGov();
            var studyRepo = new StudyRepository(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}");
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            var pubMedClient = new Scrapers.Services.PubMedClient(new HttpClient());
            var pubMedScraperService = new PubMedScraperService(Context, pubMedClient);

            var coordinator = new IngestionCoordinatorService(clinicalTrialsIngestionService, pubMedScraperService);

            await coordinator.FullScraperPipelineAsync(5);

            var studyCount = await Context.Studies.CountAsync();
            var investigatorCount = await Context.Investigators.CountAsync();
            Assert.IsTrue(studyCount > 0, "No studies found after clinical trials ingestion.");
            Assert.IsTrue(investigatorCount > 0, "No investigators found after clinical trials ingestion.");

            // Verify PubMed crawl was attempted (crawl timestamp updated)
            var crawledInvestigators = await Context.Investigators
                .Where(i => i.LastSuccessfulPubmedCrawl != null)
                .CountAsync();
            Assert.IsTrue(crawledInvestigators > 0, "No investigators had PubMed crawl timestamps updated.");
        }
    }
}
