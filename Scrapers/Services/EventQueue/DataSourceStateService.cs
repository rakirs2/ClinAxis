using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.EventQueue;

/// <summary>
/// Manages data source synchronization state for resumable scraping.
/// </summary>
public sealed class DataSourceStateService : IDataSourceStateService
{
    private readonly string _connectionString;

    public DataSourceStateService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task UpdateLastSyncAsync(string sourceName, DateTime timestamp, string? hash = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var state = await context.DataSourceStates
            .FirstOrDefaultAsync(s => s.SourceName == sourceName, cancellationToken: ct)
            .ConfigureAwait(false);

        if (state == null)
        {
            state = new DataSourceStateEntity { SourceName = sourceName };
            context.DataSourceStates.Add(state);
        }

        state.LastSyncTimestamp = timestamp;
        state.LastSyncHash = hash;
        state.Status = "idle";
        state.ErrorMessage = null;
        state.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task SetStatusAsync(string sourceName, string status, string? errorMessage = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var state = await context.DataSourceStates
            .FirstOrDefaultAsync(s => s.SourceName == sourceName, cancellationToken: ct)
            .ConfigureAwait(false);

        if (state == null)
        {
            state = new DataSourceStateEntity { SourceName = sourceName };
            context.DataSourceStates.Add(state);
        }

        state.Status = status;
        state.ErrorMessage = errorMessage;
        state.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<DataSourceStateEntity?> GetStateAsync(string sourceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        return await context.DataSourceStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SourceName == sourceName, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    public async Task<List<DataSourceStateEntity>> GetAllStatesAsync(CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        return await context.DataSourceStates
            .AsNoTracking()
            .OrderBy(s => s.SourceName)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task InitializeSourceAsync(string sourceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var exists = await context.DataSourceStates
            .AnyAsync(s => s.SourceName == sourceName, cancellationToken: ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            var state = new DataSourceStateEntity
            {
                SourceName = sourceName,
                Status = "idle",
                UpdatedAt = DateTime.UtcNow
            };

            context.DataSourceStates.Add(state);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task UpdateNextScheduledRunAsync(string sourceName, DateTime? nextRun, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var state = await context.DataSourceStates
            .FirstOrDefaultAsync(s => s.SourceName == sourceName, cancellationToken: ct)
            .ConfigureAwait(false);

        if (state == null)
        {
            state = new DataSourceStateEntity { SourceName = sourceName };
            context.DataSourceStates.Add(state);
        }

        state.NextScheduledRun = nextRun;
        state.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
