using System;

namespace Scrapers.Services;

/// <summary>
/// Bounded memo-cache for <see cref="MeSHMatcher"/> results (issue #335).
/// Repeated terms (e.g. "diabetes") dominate ingest workloads; the cache lets
/// a repeat call skip BERT inference and the 61k-embedding cosine scan.
/// </summary>
internal sealed class MeSHMatchCache
{
    // Cap raised 50k -> 250k (issue #434): the full-corpus backfill's unique-term
    // vocabulary (~150-250k) is larger than 50k, so the old cap cleared ~4x during
    // the remaining sweep and re-paid inference for every hot term each time
    // (~300-500ms each on the 2-vCPU droplet). 250k entries ≈ 30-40MB, well under
    // the 768m ingestion container limit, and the cap now holds the whole corpus
    // vocabulary so each term pays inference exactly once.
    private const int MaxEntries = 250_000;

    private readonly Dictionary<string, (int BestIdx, float BestScore)> _entries =
        new(StringComparer.Ordinal);

    /// <summary>Total cache hits since construction (never reset on eviction).</summary>
    public int CacheHits { get; private set; }

    // Case-sensitive: the BioBERT model is cased, so "MI" and "mi" tokenize to
    // different embeddings and must not share a cache entry (issue #355 P4-e).
    public static string NormalizeKey(string value) => value.Trim();

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
