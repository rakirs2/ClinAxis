using System.Collections.Generic;
using System;

namespace Scrapers.Services;

/// <summary>
/// Progress snapshot for a single ingested batch of clinical trial records.
/// </summary>
public sealed record IngestBatchInfo(
    int BatchNumber,
    int RecordsProcessed,
    TimeSpan Elapsed,
    IReadOnlyList<string> NctIds);

/// <summary>
/// Reports ingestion progress so it can be persisted and surfaced for triaging
/// (e.g., <c>scrape_events</c> rows and the data-source-state API).
/// </summary>
public interface INgestionProgressReporter
{
    /// <summary>Records that a discovery pass found <paramref name="studyCount"/> studies to ingest.</summary>
    Task ReportDiscoveryCompletedAsync(int studyCount, DateTime? since, CancellationToken cancellationToken = default);

    /// <summary>Records that one ingest batch completed.</summary>
    Task ReportBatchCompletedAsync(IngestBatchInfo batch, CancellationToken cancellationToken = default);

    /// <summary>Records that an entire ingest run completed.</summary>
    Task ReportRunCompletedAsync(int totalRecords, TimeSpan elapsed, CancellationToken cancellationToken = default);
}
