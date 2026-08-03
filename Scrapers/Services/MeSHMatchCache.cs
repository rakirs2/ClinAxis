using System;

namespace Scrapers.Services;

/// <summary>
/// Bounded memo-cache for <see cref="MeSHMatcher"/> results (issue #335).
/// Repeated terms (e.g. "diabetes") dominate ingest workloads; the cache lets
/// a repeat call skip BERT inference and the 26k-embedding cosine scan.
/// </summary>
internal sealed class MeSHMatchCache
{
    private const int MaxEntries = 50_000;

    private readonly Dictionary<string, (int BestIdx, float BestScore)> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Total cache hits since construction (never reset on eviction).</summary>
    public int CacheHits { get; private set; }

    public static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    public bool TryGet(string normalizedKey, out int bestIdx, out float bestScore)
    {
        if (_entries.TryGetValue(normalizedKey, out var hit))
        {
            CacheHits++;
            bestIdx = hit.BestIdx;
            bestScore = hit.BestScore;
            return true;
        }

        bestIdx = -1;
        bestScore = 0f;
        return false;
    }

    public void Add(string normalizedKey, int bestIdx, float bestScore)
    {
        if (_entries.Count >= MaxEntries)
        {
            // Bounded memo-cache: on overflow drop everything and keep only the
            // current term. Real workloads repeat a small hot set, so a cold
            // restart still skips the bulk of inference.
            _entries.Clear();
        }

        _entries[normalizedKey] = (bestIdx, bestScore);
    }
}
