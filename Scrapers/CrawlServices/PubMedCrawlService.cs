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

public class PubMedCrawlService : BackgroundService
{
    private readonly PubMedScraperService _pubmedScraper;
    private readonly StudyRepository _repository;
    private readonly ILogger<PubMedCrawlService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private static readonly Action<ILogger, Exception?> _starting =
        LoggerMessage.Define(LogLevel.Information, 0, "PubMedCrawlService starting");

    private static readonly Action<ILogger, long, Exception?> _eventClaimed =
        LoggerMessage.Define<long>(LogLevel.Information, 1, "PubMed crawl starting (event {EventId})");

    private static readonly Action<ILogger, int, long, Exception?> _eventComplete =
        LoggerMessage.Define<int, long>(LogLevel.Information, 2, "PubMed crawl complete: {Count} papers (event {EventId})");

    private static readonly Action<ILogger, Exception?> _crawlFailed =
        LoggerMessage.Define(LogLevel.Error, 3, "PubMed crawl failed");

    public PubMedCrawlService(
        PubMedScraperService pubmedScraper,
        StudyRepository repository,
        ILogger<PubMedCrawlService> logger)
    {
        _pubmedScraper = pubmedScraper ?? throw new ArgumentNullException(nameof(pubmedScraper));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _starting(_logger, null);

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _repository.EnsureSchemaAsync(stoppingToken).ConfigureAwait(false);

                PipelineEventEntity? evt = await _repository
                    .ClaimNextEventAsync(["study.updated"], stoppingToken)
                    .ConfigureAwait(false);

                if (evt == null)
                {
                    await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                _eventClaimed(_logger, evt.Id, null);

                var now = DateTime.UtcNow;
                await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
                {
                    SourceName = "PubMed",
                    LastStartedAt = now,
                    Status = "Running"
                }, stoppingToken).ConfigureAwait(false);

                var count = await _pubmedScraper
                    .IngestPubMedPapersAsync(stoppingToken)
                    .ConfigureAwait(false);

                await _repository.UpsertSourceCrawlStateAsync(new SourceCrawlStateEntity
                {
                    SourceName = "PubMed",
                    LastStartedAt = now,
                    LastSuccessAt = DateTime.UtcNow,
                    Status = "Idle"
                }, stoppingToken).ConfigureAwait(false);

                await _repository.CompleteEventAsync(evt.Id, stoppingToken).ConfigureAwait(false);

                await _repository.EnqueueEventAsync(new PipelineEventEntity
                {
                    EventType = "pubmed.complete",
                    Source = "PubMed",
                    Payload = null,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                }, stoppingToken).ConfigureAwait(false);

                _eventComplete(_logger, count, evt.Id, null);
            }
            catch (HttpRequestException ex)
            {
                _crawlFailed(_logger, ex);
            }
            catch (DbException ex)
            {
                _crawlFailed(_logger, ex);
            }
            catch (InvalidOperationException ex)
            {
                _crawlFailed(_logger, ex);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
