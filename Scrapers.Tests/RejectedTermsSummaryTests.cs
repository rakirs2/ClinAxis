using Scrapers.Persistence;

namespace Scrapers.Tests;

[TestClass]
public sealed class RejectedTermsSummaryTests
{
    private static RejectedTermRow Row(bool sideB, float similarity, bool accepted = false) =>
        new(sideB, similarity, accepted);

    [TestMethod]
    public void Compute_EmptyInput_HasZeroCounts()
    {
        var summary = RejectedTermsSummary.Compute(Array.Empty<RejectedTermRow>());

        Assert.AreEqual(0, summary.Total);
        Assert.AreEqual(0, summary.Accepted);
        Assert.AreEqual(0, summary.Rejected);
        Assert.AreEqual(0, summary.SimilarityBands.Values.Sum());
    }

    [TestMethod]
    public void Compute_CountsAcceptedAndRejected()
    {
        var rows = new[]
        {
            Row(sideB: true, similarity: 0.9f, accepted: true),
            Row(sideB: false, similarity: 0.4f),
            Row(sideB: true, similarity: 0.8f, accepted: true),
            Row(sideB: false, similarity: 0.3f)
        };

        var summary = RejectedTermsSummary.Compute(rows);

        Assert.AreEqual(4, summary.Total);
        Assert.AreEqual(2, summary.Accepted);
        Assert.AreEqual(2, summary.Rejected);
    }

    [TestMethod]
    public void Compute_SimilarityBands_BucketBoundariesRespectThreshold()
    {
        var rows = new[]
        {
            Row(false, 0.49f), // <0.50
            Row(false, 0.50f), // 0.50-0.65
            Row(false, 0.6499f), // 0.50-0.65
            Row(false, 0.65f), // 0.65-0.80
            Row(false, 0.7999f), // 0.65-0.80
            Row(false, 0.80f), // 0.80-1.00
            Row(false, 1.00f)  // 0.80-1.00
        };

        var summary = RejectedTermsSummary.Compute(rows);

        Assert.AreEqual(1, summary.SimilarityBands["0.00-0.50"]);
        Assert.AreEqual(2, summary.SimilarityBands["0.50-0.65"]);
        Assert.AreEqual(2, summary.SimilarityBands["0.65-0.80"]);
        Assert.AreEqual(2, summary.SimilarityBands["0.80-1.00"]);
        Assert.AreEqual(7, summary.Total);
    }

    [TestMethod]
    public void Compute_AcceptedFlagDrivesTotalsRegardlessOfMatchFlag()
    {
        var rows = new[]
        {
            Row(sideB: false, similarity: 0.7f, accepted: true),
            Row(sideB: true, similarity: 0.7f, accepted: true)
        };

        var summary = RejectedTermsSummary.Compute(rows);

        Assert.AreEqual(2, summary.Accepted);
        Assert.AreEqual(0, summary.Rejected);
    }
}
