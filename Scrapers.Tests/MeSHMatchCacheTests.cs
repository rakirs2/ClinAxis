using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class MeSHMatchCacheTests
{
    [TestMethod]
    public void TryGet_Miss_ReturnsFalseWithoutHit()
    {
        var cache = new MeSHMatchCache();

        bool found = cache.TryGet("diabetes", out int bestIdx, out float bestScore);

        Assert.IsFalse(found);
        Assert.AreEqual(-1, bestIdx);
        Assert.AreEqual(0f, bestScore);
        Assert.AreEqual(0, cache.CacheHits);
    }

    [TestMethod]
    public void Add_ThenTryGet_ReturnsStoredValues()
    {
        var cache = new MeSHMatchCache();
        cache.Add("diabetes", 42, 0.87f);

        bool found = cache.TryGet("diabetes", out int bestIdx, out float bestScore);

        Assert.IsTrue(found);
        Assert.AreEqual(42, bestIdx);
        Assert.AreEqual(0.87f, bestScore);
        Assert.AreEqual(1, cache.CacheHits);
    }

    [TestMethod]
    public void TryGet_HitsOnlyIncrementCounter_NotOnMiss()
    {
        var cache = new MeSHMatchCache();
        cache.Add("diabetes", 1, 1f);

        cache.TryGet("diabetes", out _, out _);
        cache.TryGet("diabetes", out _, out _);
        cache.TryGet("hypertension", out _, out _);

        Assert.AreEqual(2, cache.CacheHits);
    }

    [TestMethod]
    public void NormalizeKey_TrimsButPreservesCase()
    {
        Assert.AreEqual("Diabetes", MeSHMatchCache.NormalizeKey("  Diabetes  "));
        Assert.AreEqual("Type 2 DIABETES", MeSHMatchCache.NormalizeKey("Type 2 DIABETES"));
    }

    [TestMethod]
    public void TryGet_MatchesWhitespaceVariants_NotCaseVariants()
    {
        var cache = new MeSHMatchCache();
        cache.Add(MeSHMatchCache.NormalizeKey("diabetes"), 7, 0.9f);

        Assert.IsTrue(cache.TryGet(MeSHMatchCache.NormalizeKey("  diabetes "), out int idxB, out _));
        Assert.IsFalse(cache.TryGet(MeSHMatchCache.NormalizeKey("Diabetes"), out _, out _),
            "BioBERT is cased: case variants tokenize differently and must not share a cache entry");
        Assert.IsFalse(cache.TryGet("  diabetes ", out _, out _), "Raw keys must be normalized by the caller");

        Assert.AreEqual(7, idxB);
    }

    [TestMethod]
    public void Add_BeyondCap_StaysBoundedAndStillWorks()
    {
        var cache = new MeSHMatchCache();
        for (int i = 0; i < 50_001; i++)
        {
            cache.Add($"term-{i}", i, 1f);
        }

        cache.Add("diabetes", 99, 0.5f);

        Assert.IsTrue(cache.TryGet("diabetes", out int bestIdx, out _));
        Assert.AreEqual(99, bestIdx);

        cache.TryGet("diabetes", out _, out _);
        cache.TryGet("term-50000", out _, out _);
        Assert.AreEqual(3, cache.CacheHits, "Hits must keep counting across cache clears");
    }
}
