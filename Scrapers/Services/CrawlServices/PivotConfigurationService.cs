using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.CrawlServices;

/// <summary>
/// Loads and manages pivot configuration from the database.
/// Allows ops to enable/disable pivots and configure cache TTL without code changes.
/// </summary>
public sealed class PivotConfigurationService
{
    private readonly string _connectionString;
    private Dictionary<string, ScraperPivotEntity> _config = new();
    private DateTime _lastLoaded = DateTime.MinValue;
    private const int ConfigCacheDurationSeconds = 60;

    public PivotConfigurationService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Get pivot configuration, loading from DB if cache is stale.
    /// </summary>
    public async Task<ScraperPivotEntity?> GetPivotConfigAsync(string pivotName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pivotName))
            throw new ArgumentException("Pivot name cannot be null or empty", nameof(pivotName));

        await EnsureConfigLoadedAsync(ct).ConfigureAwait(false);
        return _config.TryGetValue(pivotName, out var config) ? config : null;
    }

    /// <summary>
    /// Check if a pivot is enabled.
    /// </summary>
    public async Task<bool> IsPivotEnabledAsync(string pivotName, CancellationToken ct = default)
    {
        var config = await GetPivotConfigAsync(pivotName, ct).ConfigureAwait(false);
        return config?.Enabled ?? false;
    }

    /// <summary>
    /// Get all pivot configurations.
    /// </summary>
    public async Task<List<ScraperPivotEntity>> GetAllPivotConfigsAsync(CancellationToken ct = default)
    {
        await EnsureConfigLoadedAsync(ct).ConfigureAwait(false);
        return _config.Values.ToList();
    }

    /// <summary>
    /// Get cache TTL for a pivot (default 90 days if not configured).
    /// </summary>
    public async Task<int> GetCacheTtlDaysAsync(string pivotName, CancellationToken ct = default)
    {
        var config = await GetPivotConfigAsync(pivotName, ct).ConfigureAwait(false);
        return config?.CacheTtlDays ?? 90;
    }

    /// <summary>
    /// Refresh config from database (bypass cache).
    /// </summary>
    public async Task RefreshConfigAsync(CancellationToken ct = default)
    {
        _lastLoaded = DateTime.MinValue;  // Force reload
        await EnsureConfigLoadedAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureConfigLoadedAsync(CancellationToken ct)
    {
        // Only reload if cache is stale
        if (DateTime.UtcNow - _lastLoaded < TimeSpan.FromSeconds(ConfigCacheDurationSeconds))
            return;

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var configs = await context.ScraperPivots
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        _config = configs.ToDictionary(c => c.Name);
        _lastLoaded = DateTime.UtcNow;
    }
}
