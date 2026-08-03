using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Services.Backfill;

/// <summary>
/// DB-facing half of the full-corpus sweep lifecycle (the planning half is the pure
/// <see cref="BackfillChunkPlanner"/>). Owns the queue bookkeeping and the completion
/// reconciliation:
///  - chunk queue snapshot (active vs dead-lettered chunks)
///  - completed chunk windows (for skip-on-replan)
///  - sweep completion: marks studies that CT.gov no longer has as removed (never deletes)
/// </summary>
public sealed class BackfillCoordinatorService
{
    private const string BackfillEventType = "studies.backfill";
    private readonly string _connectionString;

    public BackfillCoordinatorService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<ChunkQueueSnapshot> GetChunkQueueSnapshotAsync(CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var active = await context.PipelineEvents
            .CountAsync(e => e.EventType == BackfillEventType && (e.Status == "pending" || e.Status == "processing"), ct)
            .ConfigureAwait(false);
        var deadLettered = await context.PipelineEvents
            .CountAsync(e => e.EventType == BackfillEventType && e.Status == "dead-letter", ct)
            .ConfigureAwait(false);

        return new ChunkQueueSnapshot(active, deadLettered);
    }

    public async Task<IReadOnlyList<(DateOnly From, DateOnly To)>> GetCompletedChunkWindowsAsync(CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var completedEvents = await context.PipelineEvents
            .Where(e => e.EventType == BackfillEventType && e.Status == "completed")
            .Select(e => e.Data)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var windows = new List<(DateOnly From, DateOnly To)>();
        foreach (var data in completedEvents)
        {
            if (BackfillEventPayload.TryParse(data, out var payload) && payload is not null)
            {
                windows.Add((payload.DateFrom, payload.DateTo));
            }
        }

        return windows;
    }

    /// <summary>
    /// Completes the sweep: flags every study that was not re-fetched during this sweep
    /// (never stamped, or stamped by an earlier sweep) as removed from the source.
    /// </summary>
    /// <returns>The number of newly flagged studies.</returns>
    public async Task<int> ReconcileAndCompleteAsync(DateTime sweepStartedUtc, CancellationToken ct = default)
    {
        var repo = new StudyRepository(_connectionString);
        return await repo.MarkStudiesRemovedAsync(sweepStartedUtc, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Active (pending/processing) and dead-lettered counts of <c>studies.backfill</c> chunk events.
/// </summary>
public sealed record ChunkQueueSnapshot(int PendingOrProcessing, int DeadLettered);
