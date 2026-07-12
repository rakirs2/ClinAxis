namespace Scrapers.Services.EventQueue;

/// <summary>
/// Service for tracking fetch history per study/source combination.
/// Enables deduplication by avoiding re-fetching recently-fetched data based on cache TTL.
/// </summary>
public interface ISourceFetchHistoryService
{
    /// <summary>
    /// Check if a study source should be fetched based on last fetch timestamp and cache TTL.
    /// Returns true if should fetch (either never fetched or TTL expired).
    /// Returns false if recently fetched and still within TTL.
    /// </summary>
    Task<bool> ShouldFetchAsync(string nctId, string sourceType, int cacheTtlDays, CancellationToken ct = default);

    /// <summary>
    /// Record a successful fetch for a study/source combination.
    /// Creates a new record or updates the existing one.
    /// </summary>
    Task RecordFetchAsync(string nctId, string sourceType, string? contentHash = null, CancellationToken ct = default);

    /// <summary>
    /// Get the last fetch record for a study/source combination.
    /// Returns null if never fetched.
    /// </summary>
    Task<SourceFetchRecord?> GetLastFetchAsync(string nctId, string sourceType, CancellationToken ct = default);

    /// <summary>
    /// Clear fetch history for a specific source (useful for re-triggering fresh fetches).
    /// </summary>
    Task ClearSourceHistoryAsync(string sourceType, CancellationToken ct = default);
}

/// <summary>
/// Represents a fetch record for a study/source combination.
/// </summary>
public sealed class SourceFetchRecord
{
    public int Id { get; set; }
    public string StudyNctId { get; set; } = null!;
    public string SourceType { get; set; } = null!;
    public DateTime? LastFetchTimestamp { get; set; }
    public string? ContentHash { get; set; }
}
