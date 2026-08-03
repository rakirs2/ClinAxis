using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class MeSHMatcherCacheTests
{
    private static string MeshResourcesPath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");

    [TestMethod]
    public void Match_RepeatedTerm_ReturnsIdenticalResultWithoutSecondInference()
    {
        using var matcher = new MeSHMatcher(MeshResourcesPath);
        const string term = "metastatic breast cancer";

        var first = matcher.Match(term, "condition", "NCT00000001");
        var second = matcher.Match(term, "condition", "NCT00000002");

        Assert.AreEqual(first.MeshTerm, second.MeshTerm);
        Assert.AreEqual(first.MeshCui, second.MeshCui);
        Assert.AreEqual(first.Category, second.Category);
        Assert.AreEqual(first.Similarity, second.Similarity);
        Assert.AreEqual(1, matcher.CacheHits, "Second call must come from the memo-cache, not a new inference");
    }

    [TestMethod]
    public void Match_CaseVariant_UsesSameCacheEntry()
    {
        using var matcher = new MeSHMatcher(MeshResourcesPath);

        matcher.Match("refractory hypertension");
        var variant = matcher.Match("  Refractory HYPERTENSION ");

        Assert.AreEqual(1, matcher.CacheHits, "Case/whitespace variants must hit the same normalized entry");
        Assert.IsTrue(variant.Similarity > 0f);
    }

    [TestMethod]
    public void Match_ExactName_IsCachedToo()
    {
        using var matcher = new MeSHMatcher(MeshResourcesPath);

        var first = matcher.Match("Diabetes Mellitus");
        var second = matcher.Match("Diabetes Mellitus");

        Assert.AreEqual(1f, first.Similarity);
        Assert.AreEqual(first.MeshTerm, second.MeshTerm);
        Assert.AreEqual(1, matcher.CacheHits);
    }
}
