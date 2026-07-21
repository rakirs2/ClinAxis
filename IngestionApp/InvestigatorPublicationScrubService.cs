using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.EventQueue;

namespace IngestionApp
{
    internal sealed class InvestigatorPublicationScrubService : BackgroundService
    {
        private readonly string _connectionString;
        private readonly ILogger<InvestigatorPublicationScrubService> _logger;
        private readonly IEventQueueService _eventQueueService;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        private static readonly Action<ILogger, Exception?> LogStarted =
            LoggerMessage.Define(LogLevel.Information, 0, "InvestigatorPublicationScrubService started");

        private static readonly Action<ILogger, Exception?> LogError =
            LoggerMessage.Define(LogLevel.Error, 0, "Error processing investigator scrub event");

        private static readonly Action<ILogger, Guid, Exception?> LogScrubbed =
            LoggerMessage.Define<Guid>(LogLevel.Information, 0, "Scrubbed publications for investigator {PersonId}");

        private static readonly Action<ILogger, Exception?> LogScrubFailed =
            LoggerMessage.Define(LogLevel.Warning, 0, "Failed to scrub investigator publications");

        public InvestigatorPublicationScrubService(
            string connectionString,
            ILogger<InvestigatorPublicationScrubService> logger,
            IEventQueueService eventQueueService)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LogStarted(_logger, null);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextEventAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError(_logger, ex);
                }

                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        private async Task ProcessNextEventAsync(CancellationToken cancellationToken)
        {
            var contextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options;

            using var context = new ClinicalTrialsContext(contextOptions);

            PipelineEventEntity? pipelineEvent = await context.PipelineEvents
                .Where(e => e.EventType == "investigator.discovered" && e.Status == "pending")
                .OrderBy(e => e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (pipelineEvent == null)
            {
                return;
            }

            pipelineEvent.Status = "processing";
            pipelineEvent.ClaimedAt = DateTime.UtcNow;
            pipelineEvent.ClaimedBy = nameof(InvestigatorPublicationScrubService);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var personId = Guid.Parse(pipelineEvent.Data!);
                await ScrubInvestigatorAsync(contextOptions, personId, cancellationToken).ConfigureAwait(false);

                pipelineEvent.Status = "completed";
                pipelineEvent.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Enqueue metrics enrichment for this investigator
                await _eventQueueService.EnqueueAsync("investigator.metrics", personId.ToString(), cancellationToken).ConfigureAwait(false);

                LogScrubbed(_logger, personId, null);
            }
            catch (Exception ex)
            {
                pipelineEvent.RetryCount++;
                pipelineEvent.Status = pipelineEvent.RetryCount >= 3 ? "dead-letter" : "pending";
                pipelineEvent.LastErrorAt = DateTime.UtcNow;
                pipelineEvent.ErrorMessage = ex.Message;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                LogScrubFailed(_logger, ex);
            }
        }

        private static async Task ScrubInvestigatorAsync(
            DbContextOptions<ClinicalTrialsContext> contextOptions,
            Guid personId,
            CancellationToken cancellationToken)
        {
            using var context = new ClinicalTrialsContext(contextOptions);
            await StudyRepository.ScrubInvestigatorPapersAsync(context, personId, cancellationToken).ConfigureAwait(false);
        }
    }
}
