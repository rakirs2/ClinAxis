using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Services;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class FullPipelineIntegrationTests : DbTestBase
    {
        [TestMethod]
        [TestCategory("Integration")]
        public async Task FullScraperPipeline_ClinicalTrialsIngestsSuccessfully()
        {
            // Arrange
            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 5);
            var studyRepo = new StudyRepository(ConnectionString);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            // Act - Run ingest
            await clinicalTrialsIngestionService.IngestAsync(5);

            // Assert
            var studyCount = await Context.Studies.CountAsync();
            var investigatorCount = await Context.Investigators.CountAsync();
            
            Assert.IsTrue(studyCount >= 5, "Should have ingested at least 5 studies from ClinicalTrials.gov");
            Assert.IsTrue(investigatorCount > 0, "Should have investigators from ingestion");
        }
    }
}
