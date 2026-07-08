using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;

namespace Scrapers.Tests;

[TestClass]
public sealed class ClinicalTrialsGovIntegrationTests
{
    private const int MaxAttempts = 3;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetTrialsAsync_FetchesStudyFromLiveApi() {
        IReadOnlyList<StudySummary> result = await FetchWithRetryAsync(count: 1);

        Assert.IsTrue(result.Count >= 1, "Expected at least one study from live API.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result[0].NctId), "Live study should include an NCT ID.");
    }

    private static async Task<IReadOnlyList<StudySummary>> FetchWithRetryAsync(int count)
    {
        var client = new ClinicalTrialsGov();
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await client.GetTrialsAsync(count).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt == MaxAttempts)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))).ConfigureAwait(false);
            }
        }

        throw new AssertFailedException($"Live ClinicalTrials.gov request failed after {MaxAttempts} attempts: {lastError}");
    }
}
