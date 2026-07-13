using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Persistence;

namespace IngestionApp
{
    public class InvestigatorPublicationScrubService : BackgroundService
    {
        private readonly string _connectionString;
        private readonly ILogger<InvestigatorPublicationScrubService> _logger;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        public InvestigatorPublicationScrubService(
            string connectionString,
            ILogger<InvestigatorPublicationScrubService> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InvestigatorPublicationScrubService started");

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
                    _logger.LogError(ex, "Error processing investigator scrub event");
                }

                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        private async Task ProcessNextEventAsync(CancellationToken cancellationToken)
        {
            var contextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(_connectionString)
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
            pipelineEvent.ClaimedBy = GetType().Name;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var personId = Guid.Parse(pipelineEvent.Data);
                await ScrubInvestigatorAsync(contextOptions, personId, cancellationToken).ConfigureAwait(false);

                pipelineEvent.Status = "completed";
                pipelineEvent.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Scrubbed publications for investigator {PersonId}", personId);
            }
            catch (Exception ex)
            {
                pipelineEvent.RetryCount++;
                pipelineEvent.Status = pipelineEvent.RetryCount >= 3 ? "dead-letter" : "pending";
                pipelineEvent.LastErrorAt = DateTime.UtcNow;
                pipelineEvent.ErrorMessage = ex.Message;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(ex, "Failed to scrub investigator publications");
            }
        }

        private async Task ScrubInvestigatorAsync(
            DbContextOptions<ClinicalTrialsContext> contextOptions,
            Guid personId,
            CancellationToken cancellationToken)
        {
            using var context = new ClinicalTrialsContext(contextOptions);
            await StudyRepository.ScrubInvestigatorPapersAsync(context, personId, cancellationToken).ConfigureAwait(false);
        }
    }
}
