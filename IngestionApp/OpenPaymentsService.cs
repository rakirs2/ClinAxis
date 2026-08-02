using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

internal sealed class OpenPaymentsService : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogLoopError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, "OpenPaymentsLoopError"),
            "OpenPaymentsService error");

    private readonly IEventQueueService _eventQueueService;
    private readonly CmsOpenPaymentsClient _cmsClient;
    private readonly string _connectionString;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly ILogger<OpenPaymentsService> _logger;

    private static readonly HashSet<string> AllYears = [.. Enumerable.Range(2023, 3).Select(y => y.ToString(CultureInfo.InvariantCulture))];

    public OpenPaymentsService(
        IEventQueueService eventQueueService,
        CmsOpenPaymentsClient cmsClient,
        string connectionString,
        ILogger<OpenPaymentsService> logger,
        int pollIntervalSeconds = 30)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _cmsClient = cmsClient ?? throw new ArgumentNullException(nameof(cmsClient));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollIntervalSeconds = pollIntervalSeconds;
        _serviceInstanceId = $"{Environment.MachineName}-openpayments-{Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _eventQueueService.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30), stoppingToken).ConfigureAwait(false);

                var @event = await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    eventTypes: ["cms.openpayments"],
                    stoppingToken).ConfigureAwait(false);

                if (@event == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await ProcessEventAsync(@event, stoppingToken).ConfigureAwait(false);
                    await _eventQueueService.CompleteEventAsync(@event.Id, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _eventQueueService.FailEventAsync(@event.Id, ex.ToString(), stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLoopError(_logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessEventAsync(PipelineEventEntity @event, CancellationToken ct)
    {
        if (!Guid.TryParse(@event.Data, out var personId))
            throw new InvalidOperationException($"Invalid event data: '{@event.Data}' is not a valid GUID.");

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(_connectionString).Options);

        var person = await context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);

        if (person == null || string.IsNullOrWhiteSpace(person.Npi))
            return;

        var existing = await context.OpenPayments
            .Where(o => o.InvestigatorPersonId == person.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        context.OpenPayments.RemoveRange(existing);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var year in AllYears)
        {
            var researchRecords = await _cmsClient.GetResearchPaymentsByNpiAsync(person.Npi, year, ct).ConfigureAwait(false);
            var generalRecords = await _cmsClient.GetGeneralPaymentsByNpiAsync(person.Npi, year, ct).ConfigureAwait(false);
            var ownershipRecords = await _cmsClient.GetOwnershipByNpiAsync(person.Npi, year, ct).ConfigureAwait(false);

            StoreRecords(context, person.Id, year, "research", researchRecords);
            StoreRecords(context, person.Id, year, "general", generalRecords);
            StoreRecords(context, person.Id, year, "ownership", ownershipRecords);
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static void StoreRecords(ClinicalTrialsContext context, Guid personId, string year, string paymentType, List<OpenPaymentRecord> records)
    {
        var dataYear = int.Parse(year, CultureInfo.InvariantCulture);

        foreach (var r in records)
        {
            DateTime? paymentDate = null;
            if (r.PaymentDateString != null && DateTimeOffset.TryParse(r.PaymentDateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                paymentDate = dto.UtcDateTime;

            context.OpenPayments.Add(new OpenPaymentEntity
            {
                InvestigatorPersonId = personId,
                DataYear = dataYear,
                PaymentType = paymentType,
                PaymentAmount = r.PaymentAmount,
                PaymentDate = paymentDate,
                PayorName = r.PayorName,
                NatureOfPayment = r.NatureOfPayment,
                FormOfPayment = r.FormOfPayment,
                StudyName = r.StudyName,
                ClinicalTrialsId = r.ClinicalTrialsId,
                ContextOfResearch = r.ContextOfResearch,
                ProductCategory = r.ProductCategory,
                ProductName = r.ProductName,
                RecordId = r.RecordId ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
