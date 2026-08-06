using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class BestPiScorerTests
{
    private static readonly Guid Person1 = Guid.NewGuid();
    private static readonly Guid Person2 = Guid.NewGuid();
    private static readonly string[] OncologyCategories = ["oncology", "hematology"];
    private static readonly string[] CardiologyCategories = ["cardiology"];
    private static readonly string[] OncologyRequest = ["oncology"];
    private static readonly string[] UsRegions = ["United States"];
    private static readonly string[] CanadaRegions = ["Canada"];
    private static readonly string[] NoCategories = [];

    private static BestPiScorer.CandidateSignals Candidate(
        Guid uuid, string? name = null, int studyCount = 0,
        double? completionRate = null, double? velocity = null,
        int? hIndex = null, int? paperCount = null,
        IReadOnlyCollection<string>? categories = null,
        IReadOnlyCollection<string>? regions = null)
    {
        return new BestPiScorer.CandidateSignals(
            uuid, name, null, studyCount, completionRate, velocity, hIndex, paperCount,
            categories ?? NoCategories, regions ?? NoCategories);
    }

    // ---- Experience ----

    [TestMethod]
    public void ExperienceScore_ScalesLinearlyToCap()
    {
        Assert.AreEqual(0.25, BestPiScorer.ExperienceScore(5));
        Assert.AreEqual(1.0, BestPiScorer.ExperienceScore(20));
        Assert.AreEqual(1.0, BestPiScorer.ExperienceScore(100));
        Assert.AreEqual(0.0, BestPiScorer.ExperienceScore(0));
    }

    // ---- Completion ----

    [TestMethod]
    public void CompletionScore_PassesRateThroughClamped()
    {
        Assert.AreEqual(0.9, BestPiScorer.CompletionScore(0.9));
        Assert.AreEqual(1.0, BestPiScorer.CompletionScore(1.2));
        Assert.AreEqual(0.0, BestPiScorer.CompletionScore(-0.5));
        Assert.IsNull(BestPiScorer.CompletionScore(null));
    }

    // ---- Condition fit ----

    [TestMethod]
    public void ConditionFitScore_FullPartialAndNone()
    {
        Assert.AreEqual(1.0, BestPiScorer.ConditionFitScore(OncologyRequest, OncologyCategories));
        Assert.AreEqual(0.5, BestPiScorer.ConditionFitScore(["oncology", "cardiology"], ["oncology"]));
        Assert.AreEqual(0.0, BestPiScorer.ConditionFitScore(OncologyRequest, CardiologyCategories));
    }

    [TestMethod]
    public void ConditionFitScore_IsCaseInsensitive()
    {
        Assert.AreEqual(1.0, BestPiScorer.ConditionFitScore(["Oncology"], ["ONCOLOGY"]));
    }

    [TestMethod]
    public void ConditionFitScore_NoRequestedCategories_Abstains()
    {
        Assert.IsNull(BestPiScorer.ConditionFitScore(NoCategories, OncologyCategories));
    }

    // ---- Velocity ----

    [TestMethod]
    public void VelocityScore_CapsAtDefaultWhenNoTarget()
    {
        Assert.AreEqual(0.5, BestPiScorer.VelocityScore(10, null));
        Assert.AreEqual(1.0, BestPiScorer.VelocityScore(20, null));
        Assert.AreEqual(1.0, BestPiScorer.VelocityScore(50, null));
    }

    [TestMethod]
    public void VelocityScore_ScalesAgainstEnrollmentTarget()
    {
        // 360 participants over a 3-year trial = 10/month expectation
        Assert.AreEqual(0.5, BestPiScorer.VelocityScore(5, 360));
        Assert.AreEqual(1.0, BestPiScorer.VelocityScore(10, 360));
    }

    [TestMethod]
    public void VelocityScore_MissingVelocity_Abstains()
    {
        Assert.IsNull(BestPiScorer.VelocityScore(null, 360));
    }

    // ---- Publication ----

    [TestMethod]
    public void PublicationScore_CombinesHIndexAndPapers()
    {
        var full = BestPiScorer.PublicationScore(25, 100);
        Assert.IsNotNull(full);
        Assert.AreEqual(0.6 * 0.5 + 0.4 * 0.5, full!.Value, 0.0001);
    }

    [TestMethod]
    public void PublicationScore_OneSubsignalMissing_UsesAvailableWeight()
    {
        var hOnly = BestPiScorer.PublicationScore(50, null);
        var pOnly = BestPiScorer.PublicationScore(null, 200);

        Assert.AreEqual(1.0, hOnly!.Value, 0.0001, "hIndex-only must be scored on its own weight");
        Assert.AreEqual(1.0, pOnly!.Value, 0.0001, "paperCount-only must be scored on its own weight");
    }

    [TestMethod]
    public void PublicationScore_BothMissing_Abstains()
    {
        Assert.IsNull(BestPiScorer.PublicationScore(null, null));
    }

    // ---- Geographic ----

    [TestMethod]
    public void GeographicScore_ExactCountryMatch()
    {
        Assert.AreEqual(1.0, BestPiScorer.GeographicScore("United States", UsRegions));
        Assert.AreEqual(0.0, BestPiScorer.GeographicScore("United States", CanadaRegions));
    }

    [TestMethod]
    public void GeographicScore_IsCaseInsensitive()
    {
        Assert.AreEqual(1.0, BestPiScorer.GeographicScore("united states", UsRegions));
    }

    [TestMethod]
    public void GeographicScore_NoRequestOrNoData_Abstains()
    {
        Assert.IsNull(BestPiScorer.GeographicScore(null, UsRegions));
        Assert.IsNull(BestPiScorer.GeographicScore("United States", NoCategories));
    }

    // ---- Total scoring ----

    [TestMethod]
    public void Score_AllFactorsPresent_UsesWeightedMean()
    {
        var candidate = Candidate(Person1, "A", studyCount: 20, completionRate: 1.0, velocity: 20,
            hIndex: 50, paperCount: 200, categories: OncologyCategories, regions: UsRegions);
        var request = new BestPiScorer.RecommendationRequest(OncologyRequest, "United States", null, null);

        var result = BestPiScorer.Score(candidate, request);

        Assert.AreEqual(1.0, result.Score, 0.0001);
        Assert.AreEqual(1.0, result.Factors.Experience!.Value);
        Assert.AreEqual(1.0, result.Factors.Completion!.Value);
        Assert.AreEqual(1.0, result.Factors.ConditionFit!.Value);
        Assert.AreEqual(1.0, result.Factors.Velocity!.Value);
        Assert.AreEqual(1.0, result.Factors.Publication!.Value);
        Assert.AreEqual(1.0, result.Factors.Geographic!.Value);
    }

    [TestMethod]
    public void Score_MissingFactors_RenormalizeWeights()
    {
        // Only experience + condition fit present: total = (0.15*0.5 + 0.25*1.0) / (0.15+0.25)
        var candidate = Candidate(Person1, "A", studyCount: 10, categories: OncologyCategories);
        var request = new BestPiScorer.RecommendationRequest(OncologyRequest, null, null, null);

        var result = BestPiScorer.Score(candidate, request);

        Assert.AreEqual((0.15 * 0.5 + 0.25 * 1.0) / 0.4, result.Score, 0.0001);
        Assert.IsNull(result.Factors.Completion);
        Assert.IsNull(result.Factors.Velocity);
        Assert.IsNull(result.Factors.Publication);
        Assert.IsNull(result.Factors.Geographic);
    }

    [TestMethod]
    public void Score_GeographicMismatch_PullsScoreDownNotUp()
    {
        var candidate = Candidate(Person1, "A", studyCount: 20, completionRate: 1.0,
            categories: OncologyCategories, regions: CanadaRegions);
        var request = new BestPiScorer.RecommendationRequest(OncologyRequest, "United States", null, null);

        var result = BestPiScorer.Score(candidate, request);

        Assert.AreEqual(0.0, result.Factors.Geographic!.Value);
        Assert.IsTrue(result.Score < 1.0, "Wrong-region candidate must score below a perfect match");
    }

    [TestMethod]
    public void Score_ExperienceAlwaysPresent_ProvidesFloor()
    {
        var candidate = Candidate(Person1, "A", studyCount: 2);
        var request = new BestPiScorer.RecommendationRequest(NoCategories, null, null, null);

        var result = BestPiScorer.Score(candidate, request);

        Assert.AreEqual(0.1, result.Score, 0.0001, "With all other factors abstaining, total = experience");
    }

    // ---- Ranking ----

    [TestMethod]
    public void Rank_OrdersByScoreDescending()
    {
        var strong = Candidate(Person1, "Strong", studyCount: 20, completionRate: 1.0, categories: OncologyCategories);
        var weak = Candidate(Person2, "Weak", studyCount: 1, completionRate: 0.0, categories: CardiologyCategories);
        var request = new BestPiScorer.RecommendationRequest(OncologyRequest, null, null, null);

        var ranked = BestPiScorer.Rank([weak, strong], request);

        Assert.AreEqual(2, ranked.Count);
        Assert.AreEqual(Person1, ranked[0].Candidate.Uuid);
        Assert.AreEqual(Person2, ranked[1].Candidate.Uuid);
        Assert.IsTrue(ranked[0].Score > ranked[1].Score);
    }

    [TestMethod]
    public void Rank_TieBreaksByStudyCountThenName()
    {
        var sameA = Candidate(Person1, "Alpha", studyCount: 5);
        var sameB = Candidate(Person2, "Beta", studyCount: 5);
        var sameC = Candidate(Guid.NewGuid(), "Gamma", studyCount: 3);
        var request = new BestPiScorer.RecommendationRequest(NoCategories, null, null, null);

        var ranked = BestPiScorer.Rank([sameC, sameB, sameA], request);

        CollectionAssert.AreEqual(new[] { "Alpha", "Beta", "Gamma" }, ranked.Select(r => r.Candidate.Name).ToList());
    }

    [TestMethod]
    public void Rank_EmptyCandidates_ReturnsEmpty()
    {
        var ranked = BestPiScorer.Rank([], new BestPiScorer.RecommendationRequest(NoCategories, null, null, null));

        Assert.AreEqual(0, ranked.Count);
    }
}
