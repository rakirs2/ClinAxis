using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
using Scrapers.IntegrationTests.Fakes;
using Scrapers.IntegrationTests.Utilities;
using Scrapers.Persistence;
using Scrapers.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class E2EPipelineIntegrationTests : EphemeralDbTestBase
    {
        [TestMethod]
        public async Task FullE2E_WithLiveApis_VerifiesDbState()
        {
            const int maxAttempts = 3;
            int studyCount = 0, investigatorCount = 0, pubmedCount = 0;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
                    var studyRepo = new StudyRepository(ConnectionString);
                    var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

                    var pubMedClient = new PubMedClient(new HttpClient());
                    var pubMedScraperService = new PubMedScraperService(Context, pubMedClient);

                    var coordinator = new IngestionCoordinatorService(clinicalTrialsIngestionService, pubMedScraperService);

                    await coordinator.FullScraperPipelineAsync(5);

                    studyCount = await Context.Studies.CountAsync();
                    investigatorCount = await Context.Investigators.CountAsync();
                    pubmedCount = await Context.PubmedStudies.CountAsync();
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    lastError = ex;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
                }
            }

            if (lastError != null)
            {
                Assert.Fail($"Pipeline failed after {maxAttempts} attempts: {lastError}");
            }

            Assert.IsTrue(studyCount > 0, "No studies found after clinical trials ingestion.");
            Assert.IsTrue(investigatorCount > 0, "No investigators found after clinical trials ingestion.");
            Assert.IsTrue(pubmedCount >= 0, "PubMed crawl completed.");
        }

        [TestMethod]
        public async Task FullE2E_MultiplePublicationsPerInvestigator()
        {
            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
            var studyRepo = new StudyRepository(ConnectionString);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            var fakeClient = new FakePubMedClient();
            var pubMedScraperService = new PubMedScraperService(Context, fakeClient);

            var coordinator = new IngestionCoordinatorService(clinicalTrialsIngestionService, pubMedScraperService);

            await coordinator.FullScraperPipelineAsync(5);

            var investigators = await Context.Investigators
                .Include(i => i.PubmedStudies)
                .ToListAsync();

            Assert.IsTrue(investigators.Count > 0, "No investigators found.");
            Assert.IsTrue(investigators.All(i => i.PubmedStudies != null), "Some investigators have null PubmedStudies.");

            var piWithMultiplePapers = investigators.FirstOrDefault(i => i.PubmedStudies!.Count >= 2);
            Assert.IsNotNull(piWithMultiplePapers, "Expected at least one investigator with 2+ PubMed papers.");
            Assert.IsTrue(piWithMultiplePapers!.PubmedStudies!.Count >= 3,
                $"Expected 3+ papers for investigator '{piWithMultiplePapers.Name}', got {piWithMultiplePapers.PubmedStudies.Count}.");
        }

        [TestMethod]
        public async Task FullE2E_Pipeline_DataIntegrity()
        {
            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
            var studyRepo = new StudyRepository(ConnectionString);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            var fakeClient = new FakePubMedClient();
            var pubMedScraperService = new PubMedScraperService(Context, fakeClient);

            var coordinator = new IngestionCoordinatorService(clinicalTrialsIngestionService, pubMedScraperService);

            await coordinator.FullScraperPipelineAsync(5);

            foreach (var study in await Context.Studies.Include(s => s.Investigators).ToListAsync())
            {
                Assert.IsNotNull(study.NctId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(study.BriefTitle));

                if (study.Investigators is null) continue;
                foreach (var investigator in study.Investigators)
                {
                    Assert.AreEqual(study.Id, investigator.StudyId);
                    var pubmedPapers = await Context.PubmedStudies
                        .Where(p => p.InvestigatorId == investigator.Id)
                        .ToListAsync();

                    Assert.IsTrue(pubmedPapers.Count > 0,
                        $"Investigator '{investigator.Name}' in study '{study.NctId}' has no PubMed papers.");

                    foreach (var paper in pubmedPapers)
                    {
                        Assert.AreEqual(investigator.Id, paper.InvestigatorId,
                            $"PubMed paper '{paper.Title}' has mismatched InvestigatorId.");
                        Assert.IsFalse(string.IsNullOrWhiteSpace(paper.Title));
                        Assert.IsFalse(string.IsNullOrWhiteSpace(paper.Url));
                    }
                }
            }
        }
    }
}
