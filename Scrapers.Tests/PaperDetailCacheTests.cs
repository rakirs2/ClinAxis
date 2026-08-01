using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class PaperDetailCacheTests
{
    private static PubMedScraperService.PaperDetail CreateDetail(string pmid)
    {
        return new PubMedScraperService.PaperDetail { Title = $"Paper {pmid}", Journal = "Journal" };
    }

    [TestMethod]
    public async Task GetOrAddAsync_FetchesOncePerPmid()
    {
        var fetchCount = 0;
        var cache = new PaperDetailCache((pmid, _) =>
        {
            fetchCount++;
            return Task.FromResult<PubMedScraperService.PaperDetail?>(CreateDetail(pmid));
        });

        var first = await cache.GetOrAddAsync("30000001");
        var second = await cache.GetOrAddAsync("30000001");

        Assert.AreSame(first, second);
        Assert.AreEqual(1, fetchCount);
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public async Task GetOrAddAsync_DistinctPmids_FetchSeparately()
    {
        var fetchCount = 0;
        var cache = new PaperDetailCache((pmid, _) =>
        {
            fetchCount++;
            return Task.FromResult<PubMedScraperService.PaperDetail?>(CreateDetail(pmid));
        });

        await cache.GetOrAddAsync("30000001");
        await cache.GetOrAddAsync("30000002");

        Assert.AreEqual(2, fetchCount);
        Assert.AreEqual(2, cache.Count);
    }

    [TestMethod]
    public async Task GetOrAddAsync_NullResult_IsNotCached()
    {
        var fetchCount = 0;
        var cache = new PaperDetailCache((_, _) =>
        {
            fetchCount++;
            return Task.FromResult<PubMedScraperService.PaperDetail?>((PubMedScraperService.PaperDetail?)null);
        });

        Assert.IsNull(await cache.GetOrAddAsync("30000001"));
        Assert.IsNull(await cache.GetOrAddAsync("30000001"));
        Assert.AreEqual(2, fetchCount);
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public async Task GetOrAddAsync_ConcurrentCalls_FetchOnce()
    {
        var fetchCount = 0;
        var cache = new PaperDetailCache(async (pmid, ct) =>
        {
            await Task.Delay(50, ct).ConfigureAwait(false);
            fetchCount++;
            return CreateDetail(pmid);
        });

        var tasks = new Task<PubMedScraperService.PaperDetail?>[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = cache.GetOrAddAsync("30000001");
        }
        var results = await Task.WhenAll(tasks);

        Assert.AreEqual(1, fetchCount);
        Assert.AreSame(results[0], results[9]);
    }

    [TestMethod]
    public async Task GetOrAddAsync_WhitespacePmid_ReturnsNullWithoutFetching()
    {
        var fetchCount = 0;
        var cache = new PaperDetailCache((pmid, _) =>
        {
            fetchCount++;
            return Task.FromResult<PubMedScraperService.PaperDetail?>(CreateDetail(pmid));
        });

        Assert.IsNull(await cache.GetOrAddAsync("  "));
        Assert.AreEqual(0, fetchCount);
    }
}
