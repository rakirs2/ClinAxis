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

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class FullPipelineIntegrationTests : Scrapers.Tests.Utilities.TestBase
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

            var investigators = await Context.Investigators.Include(i => i.PubmedStudies).ToListAsync();
            Assert.IsTrue(investigators.All(i => i.PubmedStudies != null), "Not all investigators have PubMed papers loaded.");

            foreach (var study in await Context.Studies.Include(s => s.Investigators).ToListAsync())
            {
                foreach (var investigator in study.Investigators)
                {
                    var pubmedPapers = await Context.PubmedStudies.Where(p => p.InvestigatorId == investigator.Id).ToListAsync();
                    Assert.IsTrue(pubmedPapers.Count > 0, $"Investigator '{investigator.Name}' has no PubMed papers crawled.");
                }
            }
        }
    }
}
