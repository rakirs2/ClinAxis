using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

namespace Scrapers.CrawlServices;

public class AggregationCrawlService : BackgroundService
{
    private readonly AggregationService _aggregationService;
    private readonly StudyRepository _repository;
    private readonly ILogger<AggregationCrawlService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private static readonly Action<ILogger, Exception?> _starting =
        LoggerMessage.Define(LogLevel.Information, 0, "AggregationCrawlService starting");

    private static readonly Action<ILogger, long, string, Exception?> _eventClaimed =
        LoggerMessage.Define<long, string>(LogLevel.Information, 1, "Aggregation starting (event {EventId}, type {Type})");

    private static readonly Action<ILogger, long, Exception?> _eventComplete =
        LoggerMessage.Define<long>(LogLevel.Information, 2, "Aggregation complete (event {EventId})");

    private static readonly Action<ILogger, Exception?> _aggFailed =
        LoggerMessage.Define(LogLevel.Error, 3, "Aggregation failed");

    public AggregationCrawlService(
        AggregationService aggregationService,
        StudyRepository repository,
        ILogger<AggregationCrawlService> logger)
    {
        _aggregationService = aggregationService ?? throw new ArgumentNullException(nameof(aggregationService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _starting(_logger, null);

        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _repository.EnsureSchemaAsync(stoppingToken).ConfigureAwait(false);

                PipelineEventEntity? evt = await _repository
                    .ClaimNextEventAsync(["pubmed.complete", "study.updated"], stoppingToken)
                    .ConfigureAwait(false);

                if (evt == null)
                {
                    await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                _eventClaimed(_logger, evt.Id, evt.EventType, null);

                var now = DateTime.UtcNow;
                await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
                {
                    SourceName = "Aggregation",
                    LastStartedAt = now,
                    Status = "Running"
                }, stoppingToken).ConfigureAwait(false);

                await _aggregationService
                    .AggregateAsync(stoppingToken)
                    .ConfigureAwait(false);

                await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
                {
                    SourceName = "Aggregation",
                    LastStartedAt = now,
                    LastSuccessAt = DateTime.UtcNow,
                    Status = "Idle"
                }, stoppingToken).ConfigureAwait(false);

                await _repository.CompleteEventAsync(evt.Id, stoppingToken).ConfigureAwait(false);

                _eventComplete(_logger, evt.Id, null);
            }
            catch (DbException ex)
            {
                _aggFailed(_logger, ex);
            }
            catch (InvalidOperationException ex)
            {
                _aggFailed(_logger, ex);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
