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
        [TestMethod]
        [TestCategory("Integration")]
        public async Task FullE2E_BackgroundServices_IngestAndProcess()
        {
            // Arrange
            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 5);
            var studyRepo = new StudyRepository(ConnectionString);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

            // Act - Ingest 5 studies via ClinicalTrials service
            await clinicalTrialsIngestionService.IngestAsync(5);

            // Assert - Verify ingestion was successful
            int studyCount = await Context.Studies.CountAsync();
            int investigatorCount = await Context.Investigators.CountAsync();
            
            Assert.IsTrue(studyCount >= 5, "Should have ingested at least 5 studies");
            Assert.IsTrue(investigatorCount > 0, "Should have investigators from ingestion");
            
            // Verify data integrity: all investigators linked to correct studies
            var studies = await Context.Studies.Include(s => s.Investigators).ToListAsync();
            foreach (var study in studies)
            {
                Assert.IsNotNull(study.NctId, "Study should have NCT ID");
                Assert.IsFalse(string.IsNullOrWhiteSpace(study.BriefTitle), "Study should have title");
                
                if (study.Investigators?.Count > 0)
                {
                    foreach (var investigator in study.Investigators)
                    {
                        Assert.AreEqual(study.NctId, investigator.StudyNctId, 
                            "Investigator should be linked to correct study");
                    }
                }
            }
        }

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
