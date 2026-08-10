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
    public void Match_WhitespaceVariant_UsesSameCacheEntry_CaseVariantDoesNot()
    {
        using var matcher = new MeSHMatcher(MeshResourcesPath);

        matcher.Match("refractory hypertension");
        var variant = matcher.Match("  refractory hypertension ");

        Assert.AreEqual(1, matcher.CacheHits, "Whitespace variants must hit the same normalized entry");
        Assert.IsTrue(variant.Similarity > 0f);

        matcher.Match("Refractory Hypertension");
        Assert.AreEqual(1, matcher.CacheHits,
            "BioBERT is cased: case variants tokenize differently and must not share a cache entry");
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

    [TestMethod]
    public void Matcher_SearchesEachEmbeddingOnce()
    {
        using var matcher = new MeSHMatcher(MeshResourcesPath);

        Assert.IsTrue(
            matcher.SearchCandidateCount < matcher.AliasCount,
            $"Expected duplicate aliases to be removed from the search ({matcher.SearchCandidateCount}/{matcher.AliasCount})");
    }
}
