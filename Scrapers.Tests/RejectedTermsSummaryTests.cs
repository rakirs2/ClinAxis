using Scrapers.Persistence;

namespace Scrapers.Tests;

[TestClass]
public sealed class RejectedTermsSummaryTests
{
    private static RejectedTermRow Row(bool sideA, bool sideB, float similarity, bool accepted = false) =>
        new(sideA, sideB, similarity, accepted);

    [TestMethod]
    public void Compute_EmptyInput_HasFullAgreementAndZeroCounts()
    {
        var summary = RejectedTermsSummary.Compute(Array.Empty<RejectedTermRow>());

        Assert.AreEqual(0, summary.Total);
        Assert.AreEqual(1.0, summary.Agreement);
        Assert.AreEqual(0, summary.Disagreements);
        Assert.AreEqual(0, summary.Accepted);
        Assert.AreEqual(0, summary.Rejected);
        Assert.AreEqual(0, summary.SimilarityBands.Values.Sum());
    }

    [TestMethod]
    public void Compute_AgreesAndDisagrees_CountsAndRatioCorrect()
    {
        var rows = new[]
        {
            Row(sideA: true, sideB: true, similarity: 0.9f, accepted: true), // agree, accepted
            Row(sideA: false, sideB: false, similarity: 0.4f),               // agree, rejected
            Row(sideA: true, sideB: false, similarity: 0.6f),                // disagree
            Row(sideA: false, sideB: true, similarity: 0.8f)                 // disagree
        };

        var summary = RejectedTermsSummary.Compute(rows);

        Assert.AreEqual(4, summary.Total);
        Assert.AreEqual(2, summary.Disagreements);
        Assert.AreEqual(0.5, summary.Agreement, 1e-9);
        Assert.AreEqual(1, summary.Accepted);
        Assert.AreEqual(3, summary.Rejected);
    }

    [TestMethod]
    public void Compute_SimilarityBands_BucketBoundariesRespectThreshold()
    {
        var rows = new[]
        {
            Row(false, true, 0.49f), // <0.50
            Row(false, true, 0.50f), // 0.50-0.65
            Row(false, true, 0.6499f), // 0.50-0.65
            Row(false, true, 0.65f), // 0.65-0.80
            Row(false, true, 0.7999f), // 0.65-0.80
            Row(false, true, 0.80f), // 0.80-1.00
            Row(false, true, 1.00f)  // 0.80-1.00
        };

        var summary = RejectedTermsSummary.Compute(rows);

        Assert.AreEqual(1, summary.SimilarityBands["0.00-0.50"]);
        Assert.AreEqual(2, summary.SimilarityBands["0.50-0.65"]);
        Assert.AreEqual(2, summary.SimilarityBands["0.65-0.80"]);
        Assert.AreEqual(2, summary.SimilarityBands["0.80-1.00"]);
        Assert.AreEqual(7, summary.Total);
    }

    [TestMethod]
    public void Compute_AcceptedFlagIndependentOfSideOutcomes()
    {
        var rows = new[]
        {
            Row(sideA: true, sideB: false, similarity: 0.7f, accepted: true),
            Row(sideA: false, sideB: true, similarity: 0.7f, accepted: true)
        };

        var summary = RejectedTermsSummary.Compute(rows);

        Assert.AreEqual(2, summary.Disagreements);
        Assert.AreEqual(2, summary.Accepted);
        Assert.AreEqual(0, summary.Rejected);
    }
}
