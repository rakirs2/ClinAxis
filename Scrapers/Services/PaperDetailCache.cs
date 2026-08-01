using System.Collections.Concurrent;

namespace Scrapers.Services;

/// <summary>
/// Caches fetched PubMed paper details per PMID. Papers are immutable, so each PMID
/// is only fetched once per process — this removes duplicate PubMed HTTP calls that
/// previously fired once per (person, PMID) pair during scrubbing (issue #334).
/// Only successful fetches are cached; failures are retried on the next request.
/// </summary>
internal sealed class PaperDetailCache : IDisposable
{
    private readonly Func<string, CancellationToken, Task<PubMedScraperService.PaperDetail?>> _fetcher;
    private readonly ConcurrentDictionary<string, PubMedScraperService.PaperDetail> _items = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PaperDetailCache(Func<string, CancellationToken, Task<PubMedScraperService.PaperDetail?>> fetcher)
    {
        ArgumentNullException.ThrowIfNull(fetcher);
        _fetcher = fetcher;
    }

    public async Task<PubMedScraperService.PaperDetail?> GetOrAddAsync(string pmid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pmid))
        {
            return null;
        }

        if (_items.TryGetValue(pmid, out PubMedScraperService.PaperDetail? cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_items.TryGetValue(pmid, out cached))
            {
                return cached;
            }

            PubMedScraperService.PaperDetail? detail = await _fetcher(pmid, cancellationToken).ConfigureAwait(false);
            if (detail is not null)
            {
                _items.TryAdd(pmid, detail);
            }
            return detail;
        }
        finally
        {
            _gate.Release();
        }
    }

    public int Count => _items.Count;

    public void Dispose()
    {
        _gate.Dispose();
    }
}
