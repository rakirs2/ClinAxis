using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.EventQueue;

/// <summary>
/// Manages fetch history to enable deduplication and avoid re-fetching unchanged data.
/// </summary>
public sealed class SourceFetchHistoryService : ISourceFetchHistoryService
{
    private readonly string _connectionString;

    public SourceFetchHistoryService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<bool> ShouldFetchAsync(string nctId, string sourceType, int cacheTtlDays, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nctId))
            throw new ArgumentException("NCT ID cannot be null or empty", nameof(nctId));

        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var lastFetch = await context.SourceFetchHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                h => h.StudyNctId == nctId && h.SourceType == sourceType,
                cancellationToken: ct)
            .ConfigureAwait(false);

        if (lastFetch == null)
            return true;  // Never fetched

        if (!lastFetch.LastFetchTimestamp.HasValue)
            return true;  // No valid timestamp

        var ttlExpired = DateTime.UtcNow - lastFetch.LastFetchTimestamp.Value > TimeSpan.FromDays(cacheTtlDays);
        return ttlExpired;
    }

    public async Task RecordFetchAsync(string nctId, string sourceType, string? contentHash = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nctId))
            throw new ArgumentException("NCT ID cannot be null or empty", nameof(nctId));

        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var history = await context.SourceFetchHistories
            .FirstOrDefaultAsync(
                h => h.StudyNctId == nctId && h.SourceType == sourceType,
                cancellationToken: ct)
            .ConfigureAwait(false);

        if (history == null)
        {
            history = new SourceFetchHistoryEntity
            {
                StudyNctId = nctId,
                SourceType = sourceType,
                CreatedAt = DateTime.UtcNow
            };
            context.SourceFetchHistories.Add(history);
        }

        history.LastFetchTimestamp = DateTime.UtcNow;
        history.ContentHash = contentHash;
        history.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<SourceFetchRecord?> GetLastFetchAsync(string nctId, string sourceType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nctId))
            throw new ArgumentException("NCT ID cannot be null or empty", nameof(nctId));

        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var history = await context.SourceFetchHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                h => h.StudyNctId == nctId && h.SourceType == sourceType,
                cancellationToken: ct)
            .ConfigureAwait(false);

        if (history == null)
            return null;

        return new SourceFetchRecord
        {
            Id = history.Id,
            StudyNctId = history.StudyNctId,
            SourceType = history.SourceType,
            LastFetchTimestamp = history.LastFetchTimestamp,
            ContentHash = history.ContentHash
        };
    }

    public async Task ClearSourceHistoryAsync(string sourceType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var records = await context.SourceFetchHistories
            .Where(h => h.SourceType == sourceType)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        context.SourceFetchHistories.RemoveRange(records);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
