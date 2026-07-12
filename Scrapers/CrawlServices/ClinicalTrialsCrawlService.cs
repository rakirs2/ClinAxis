using System;
using System.Data.Common;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

namespace Scrapers.CrawlServices;

public class ClinicalTrialsCrawlService : BackgroundService
{
    private readonly ClinicalTrialsIngestionService _ingestionService;
    private readonly StudyRepository _repository;
    private readonly ILogger<ClinicalTrialsCrawlService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private static readonly Action<ILogger, Exception?> _starting =
        LoggerMessage.Define(LogLevel.Information, 0, "ClinicalTrialsCrawlService starting");

    private static readonly Action<ILogger, string?, Exception?> _crawlStarting =
        LoggerMessage.Define<string?>(LogLevel.Information, 1, "ClinicalTrials crawl starting (updatedSince={UpdatedSince})");

    private static readonly Action<ILogger, int, int, Exception?> _crawlComplete =
        LoggerMessage.Define<int, int>(LogLevel.Information, 2, "ClinicalTrials crawl complete: {Count} studies, {Total} total");

    private static readonly Action<ILogger, Exception?> _crawlFailed =
        LoggerMessage.Define(LogLevel.Error, 3, "ClinicalTrials crawl failed");

    public ClinicalTrialsCrawlService(
        ClinicalTrialsIngestionService ingestionService,
        StudyRepository repository,
        ILogger<ClinicalTrialsCrawlService> logger)
    {
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _starting(_logger, null);

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var crawlStartedAt = DateTime.UtcNow;
            try
            {
                await _repository.EnsureSchemaAsync(stoppingToken).ConfigureAwait(false);

                SourceCrawlStateEntity? state = await _repository
                    .GetSourceCrawlStateAsync("ClinicalTrials", stoppingToken)
                    .ConfigureAwait(false);

                DateTime? updatedSince = state?.LastSuccessAt;
                _crawlStarting(_logger, updatedSince?.ToString("O"), null);

                await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
                {
                    SourceName = "ClinicalTrials",
                    LastStartedAt = crawlStartedAt,
                    LastSuccessAt = state?.LastSuccessAt,
                    TotalRecordsFetched = state?.TotalRecordsFetched ?? 0,
                    Status = "Running"
                }, stoppingToken).ConfigureAwait(false);

                var count = await _ingestionService
                    .IngestAsync(1000, updatedSince, stoppingToken)
                    .ConfigureAwait(false);

                var total = (state?.TotalRecordsFetched ?? 0) + count;
                await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
                {
                    SourceName = "ClinicalTrials",
                    LastStartedAt = crawlStartedAt,
                    LastSuccessAt = DateTime.UtcNow,
                    TotalRecordsFetched = total,
                    Status = "Idle"
                }, stoppingToken).ConfigureAwait(false);

                if (count > 0)
                {
                    await _repository.EnqueueEventAsync(new PipelineEventEntity
                    {
                        EventType = "study.updated",
                        Source = "ClinicalTrials",
                        Payload = null,
                        Status = "pending",
                        CreatedAt = DateTime.UtcNow
                    }, stoppingToken).ConfigureAwait(false);
                }

                _crawlComplete(_logger, count, total, null);
            }
            catch (HttpRequestException ex)
            {
                _crawlFailed(_logger, ex);
                await RecordFailureAsync(ex.Message, crawlStartedAt, stoppingToken).ConfigureAwait(false);
            }
            catch (DbException ex)
            {
                _crawlFailed(_logger, ex);
                await RecordFailureAsync(ex.Message, crawlStartedAt, stoppingToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _crawlFailed(_logger, ex);
                await RecordFailureAsync(ex.Message, crawlStartedAt, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RecordFailureAsync(string error, DateTime crawlStartedAt, CancellationToken stoppingToken)
    {
        try
        {
            await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
            {
                SourceName = "ClinicalTrials",
                LastStartedAt = crawlStartedAt,
                Status = "Failed",
                ErrorMessage = error,
                UpdatedAt = DateTime.UtcNow
            }, stoppingToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }
}
