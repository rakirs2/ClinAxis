using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
using System.Threading.Tasks;

namespace Scrapers.IntegrationTests
{
    [TestClass]
    public class PipelineRunnerIntegrationTests
    {
        private static string? _originalConnectionString;

        [TestInitialize]
        public void Init()
        {
            _originalConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_originalConnectionString != null)
                Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _originalConnectionString);
            else
                Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", null);
        }

        [TestMethod]
        public async Task RunPipeline_WithLiveApis_VerifiesRecordCounts()
        {
            var result = await PipelineRunner.RunAsync(clinicalTrialsCount: 5);

            Assert.AreEqual(5, result.StudyCount, "Expected exactly 5 studies to be ingested.");
            Assert.IsTrue(result.InvestigatorCount > 0, "Expected at least one investigator across the ingested studies.");
            Assert.IsNull(result.Errors, "No validation errors expected. Errors: " +
                (result.Errors != null ? string.Join("; ", result.Errors) : "none"));
        }
    }
}