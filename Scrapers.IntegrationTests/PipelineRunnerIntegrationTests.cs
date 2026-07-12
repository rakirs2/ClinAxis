using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Testing;
using System.Threading.Tasks;

namespace Scrapers.IntegrationTests;

[TestClass]
public class PipelineRunnerIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunPipeline_VerifiesRecordCounts()
    {
        // Arrange
        var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 5);
        var studyRepo = new StudyRepository(ConnectionString);
        var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

        // Act - Run pipeline ingestion
        await clinicalTrialsIngestionService.IngestAsync(5);

        // Assert - Verify record counts
        var studyCount = await Context.Studies.CountAsync();
        var investigatorCount = await Context.Investigators.CountAsync();
        
        Assert.IsTrue(studyCount >= 5, $"Expected at least 5 studies to be ingested, got {studyCount}");
        Assert.IsTrue(investigatorCount > 0, "Expected at least one investigator across the ingested studies");
        Assert.IsTrue(studyCount <= 100, "Sanity check: study count should be reasonable");
    }
}
