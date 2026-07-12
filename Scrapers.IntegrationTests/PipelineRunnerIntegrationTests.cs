using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;
using System.Threading.Tasks;

namespace Scrapers.IntegrationTests;

[TestClass]
public class PipelineRunnerIntegrationTests : DbTestBase
{
    // [TestMethod]
    // public async Task RunPipeline_WithLiveApis_VerifiesRecordCounts()
    // {
    //     var originalConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
    //     Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", ConnectionString);
    //     try
    //     {
    //         PipelineResult result = await PipelineRunner.RunAsync(clinicalTrialsCount: 5);
    //
    //         Assert.AreEqual(5, result.StudyCount, "Expected exactly 5 studies to be ingested.");
    //         Assert.IsTrue(result.InvestigatorCount > 0, "Expected at least one investigator across the ingested studies.");
    //         Assert.IsNull(result.Errors, "No validation errors expected. Errors: " +
    //             (result.Errors != null ? string.Join("; ", result.Errors) : "none"));
    //     }
    //     finally
    //     {
    //         Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", originalConnectionString);
    //     }
    // }
}
