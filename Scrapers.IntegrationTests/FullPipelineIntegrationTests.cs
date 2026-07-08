using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
using Scrapers.Persistence;
using Scrapers.Services;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.IntegrationTests.Utilities;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class FullPipelineIntegrationTests : EphemeralDbTestBase
    {
        [TestMethod]
        public async Task FullScraperPipeline_RunsAndVerifiesDataAsync()
        {
            var clinicalTrialsClient = new ClinicalTrialsGov();
            var studyRepo = new StudyRepository(ConnectionString);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            var pubMedScraperService = new PubMedScraperService(ConnectionString);

            var coordinator = new IngestionCoordinatorService(clinicalTrialsIngestionService, pubMedScraperService);

            await coordinator.FullScraperPipelineAsync(5);

            var studyCount = await Context.Studies.CountAsync();
            var investigatorCount = await Context.Investigators.CountAsync();
            Assert.IsTrue(studyCount > 0, "No studies found after clinical trials ingestion.");
            Assert.IsTrue(investigatorCount > 0, "No investigators found after clinical trials ingestion.");
        }
    }
}
