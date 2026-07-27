using DataApi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataApi.Tests;

[TestClass]
public sealed class InvestigatorScorerTests
{
    [TestMethod]
    public void ComputeRelevance_ZeroStudies_ReturnsZero()
    {
        var score = InvestigatorScorer.ComputeRelevance(0);
        Assert.AreEqual(0.0, score);
    }

    [TestMethod]
    public void ComputeRelevance_HighStudies_CapsAtOne()
    {
        var score = InvestigatorScorer.ComputeRelevance(100);
        Assert.AreEqual(1.0, score);
    }

    [TestMethod]
    public void ComputeExperience_AllCompleted_ReturnsHighScore()
    {
        var score = InvestigatorScorer.ComputeExperience(10, 10, 5000);
        Assert.IsTrue(score > 0.5);
        Assert.IsTrue(score <= 1.0);
    }

    [TestMethod]
    public void ComputeExperience_NoneCompleted_ReturnsLowerScore()
    {
        var completed = InvestigatorScorer.ComputeExperience(5, 0, null);
        var allCompleted = InvestigatorScorer.ComputeExperience(5, 5, 500);
        Assert.IsTrue(completed < allCompleted);
    }

    [TestMethod]
    public void ComputeExperience_NoEnrollment_StillReturnsScore()
    {
        var score = InvestigatorScorer.ComputeExperience(1, 1, null);
        Assert.IsTrue(score > 0);
    }

    [TestMethod]
    public void ComputePublication_HighHIndex_CapsAtOne()
    {
        var score = InvestigatorScorer.ComputePublication(200, 500);
        Assert.AreEqual(1.0, score);
    }

    [TestMethod]
    public void ComputePublication_NoMetrics_ReturnsZero()
    {
        var score = InvestigatorScorer.ComputePublication(null, null);
        Assert.AreEqual(0.0, score);
    }

    [TestMethod]
    public void ComputeNetwork_Zero_CapsAtZero()
    {
        var score = InvestigatorScorer.ComputeNetwork(0);
        Assert.AreEqual(0.0, score);
    }

    [TestMethod]
    public void ComputeNetwork_High_CapsAtOne()
    {
        var score = InvestigatorScorer.ComputeNetwork(100);
        Assert.AreEqual(1.0, score);
    }

    [TestMethod]
    public void ComputeTotal_AllPerfect_ReturnsOne()
    {
        var total = InvestigatorScorer.ComputeTotal(1.0, 1.0, 1.0, 1.0);
        Assert.AreEqual(1.0, total);
    }

    [TestMethod]
    public void ComputeTotal_AllZero_ReturnsZero()
    {
        var total = InvestigatorScorer.ComputeTotal(0, 0, 0, 0);
        Assert.AreEqual(0.0, total);
    }

    [TestMethod]
    public void ComputeTotal_WeightedCorrectly()
    {
        var total = InvestigatorScorer.ComputeTotal(0.5, 0.5, 0.5, 0.5);
        Assert.AreEqual(0.5, total);
    }
}
