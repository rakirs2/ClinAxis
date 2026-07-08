using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Scrapers.Tests;

[TestClass]
public sealed class ClinicalTrialsGovLiveTests
{
    private static bool LiveTestsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_LIVE_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    [TestMethod]
    [TestCategory("Live")]
    public async Task GetTrialsAsync_LiveFetchesStudy()
    {
        if (!LiveTestsEnabled)
        {
            Assert.Inconclusive("Set RUN_LIVE_TESTS=true to run live ClinicalTrials.gov tests.");
        }

        var client = new ClinicalTrialsGov();
        var result = await client.GetTrialsAsync(count: 1);

        Assert.IsTrue(result.Count >= 1, "Expected at least one study from live API.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result[0].NctId), "Live study should include an NCT ID.");
    }
}
