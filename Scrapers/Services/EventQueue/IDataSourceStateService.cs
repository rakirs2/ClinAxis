using Scrapers.Persistence.Entities;

namespace Scrapers.Services.EventQueue;

/// <summary>
/// Service for tracking data source synchronization state.
/// Enables resumable, incremental scraping by tracking last_sync_timestamp per source.
/// </summary>
public interface IDataSourceStateService
{
    /// <summary>
    /// Update the last sync timestamp and hash for a data source.
    /// </summary>
    Task UpdateLastSyncAsync(string sourceName, DateTime timestamp, string? hash = null, CancellationToken ct = default);

    /// <summary>
    /// Set the status and optional error message for a data source.
    /// </summary>
    Task SetStatusAsync(string sourceName, string status, string? errorMessage = null, CancellationToken ct = default);

    /// <summary>
    /// Get the current state of a data source.
    /// Returns null if source doesn't exist.
    /// </summary>
    Task<DataSourceStateEntity?> GetStateAsync(string sourceName, CancellationToken ct = default);

    /// <summary>
    /// Get all data source states.
    /// </summary>
    Task<List<DataSourceStateEntity>> GetAllStatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Initialize a new data source with default state.
    /// </summary>
    Task InitializeSourceAsync(string sourceName, CancellationToken ct = default);
}
