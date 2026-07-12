using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;
using Scrapers.Persistence;
using Scrapers.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence.Entities;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class E2EPipelineIntegrationTests : DbTestBase
    {
        // [TestMethod]
        // public async Task FullE2E_WithLiveApis_VerifiesDbState()
        // {
        //     var originalConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        //     Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", ConnectionString);
        //     try
        //     {
        //         PipelineResult result = await PipelineRunner.RunAsync(clinicalTrialsCount: 5);
        //         Assert.AreEqual(5, result.StudyCount);
        //         Assert.IsTrue(result.InvestigatorCount > 0);
        //         Assert.IsNull(result.Errors);
        //     }
        //     finally
        //     {
        //         Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", originalConnectionString);
        //     }
        // }

        [TestMethod]
        public async Task FullE2E_Pipeline_DataIntegrity()
        {
            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
            var studyRepo = new StudyRepository(ConnectionString);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            await clinicalTrialsIngestionService.IngestAsync(5);

            foreach (StudyEntity? study in await Context.Studies.Include(s => s.Investigators).ToListAsync())
            {
                Assert.IsNotNull(study.NctId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(study.BriefTitle));

                if (study.Investigators is null)
                {
                    continue;
                }

                foreach (InvestigatorEntity investigator in study.Investigators)
                {
                    Assert.AreEqual(study.NctId, investigator.StudyNctId);
                }
            }
        }
    }
}
