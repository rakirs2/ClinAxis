using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
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
        public async Task FullScraperPipeline_RunsAndVerifiesDataAsync()
        {
            var clinicalTrialsClient = new ClinicalTrialsGov();
            var studyRepo = new StudyRepository(ConnectionString);
            var progressReporter = new ScrapeEventProgressReporter(ConnectionString, "ClinicalTrials.gov");
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo, progressReporter);

            var pubMedScraperService = new PubMedScraperService(ConnectionString);

            var coordinator = new IngestionCoordinatorService(clinicalTrialsIngestionService, pubMedScraperService);

            await coordinator.FullScraperPipelineAsync(5);

            var studyCount = await Context.Studies.CountAsync();
            var investigatorCount = await Context.InvestigatorPersons.CountAsync();
            Assert.IsTrue(studyCount > 0, "No studies found after clinical trials ingestion.");
            Assert.IsTrue(investigatorCount > 0, "No investigators found after clinical trials ingestion.");

            // The run must record its rate: a run.completed scrape event with row count + wall time.
            var runEvents = Context.ScrapeEvents
                .Where(e => e.Source == "ClinicalTrials.gov" && e.EventType == ScrapeProgressAggregator.RunCompletedEventType)
                .ToList();
            Assert.IsTrue(runEvents.Count > 0, "Expected a run.completed scrape event after ingestion.");
            var runEvent = runEvents.OrderByDescending(e => e.Id).First();
            Assert.IsTrue(runEvent.RecordsAffected > 0, "run.completed must carry the ingested row count.");
            Assert.IsTrue(runEvent.DurationMs > 0, "run.completed must carry the run duration.");
            Assert.IsNotNull(ScrapeEtaCalculator.RatePerHour(runEvent.RecordsAffected ?? 0, runEvent.DurationMs),
                "A completed run must yield an observable ingest rate.");
        }
    }
}
