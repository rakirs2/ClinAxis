using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

internal sealed class MedicareUtilizationService : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogLoopError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, "MedicareLoopError"),
            "MedicareUtilizationService error");

    private static readonly Action<ILogger, string, Exception> LogLookupFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "MedicareLookupFailed"),
            "Medicare lookup failed for {PersonName}");

    private readonly IEventQueueService _eventQueueService;
    private readonly CmsMedicareClient _cmsClient;
    private readonly string _connectionString;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly int _dataYear;
    private readonly ILogger<MedicareUtilizationService> _logger;

    public MedicareUtilizationService(
        IEventQueueService eventQueueService,
        CmsMedicareClient cmsClient,
        string connectionString,
        ILogger<MedicareUtilizationService> logger,
        int pollIntervalSeconds = 30,
        int dataYear = 0)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _cmsClient = cmsClient ?? throw new ArgumentNullException(nameof(cmsClient));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollIntervalSeconds = pollIntervalSeconds;
        _dataYear = dataYear > 0 ? dataYear : DateTime.UtcNow.Year;
        _serviceInstanceId = $"{System.Environment.MachineName}-medicare-{System.Environment.ProcessId}";
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
                    eventTypes: ["medicare.utilization"],
                    stoppingToken).ConfigureAwait(false);

                if (@event == null)
                {
                    await ProcessManualNpiPersonsAsync(stoppingToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await ProcessMedicareUtilizationEventAsync(@event, stoppingToken).ConfigureAwait(false);
                    await _eventQueueService.CompleteEventAsync(@event.Id, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        $"{ex.GetType().Name}: {ex.Message}",
                        stoppingToken).ConfigureAwait(false);
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

    private async Task ProcessManualNpiPersonsAsync(CancellationToken ct)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString).Options);

        var manualPersons = await context.InvestigatorPersons
            .Where(p => p.Npi != null && p.MedicareLookupAttemptedAt == null)
            .Take(10)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var person in manualPersons)
        {
            try
            {
                await ProcessPersonAsync(context, person, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogLookupFailed(_logger, person.FullName, ex);
                person.MedicareLookupResult = "error";
                person.MedicareLookupAttemptedAt = DateTime.UtcNow;
                person.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (manualPersons.Count > 0)
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task ProcessMedicareUtilizationEventAsync(PipelineEventEntity @event, CancellationToken ct)
    {
        if (!Guid.TryParse(@event.Data, out var personId))
            throw new InvalidOperationException($"Invalid event data: '{@event.Data}' is not a valid GUID.");

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString).Options);

        var person = await context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);

        if (person == null)
            throw new InvalidOperationException($"InvestigatorPerson not found for id={personId}.");

        if (person.MedicareLookupAttemptedAt != null)
            return;

        await ProcessPersonAsync(context, person, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task ProcessPersonAsync(ClinicalTrialsContext context, InvestigatorPersonEntity person, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(person.Npi))
        {
            person.MedicareLookupResult = "no_npi";
            person.MedicareLookupAttemptedAt = DateTime.UtcNow;
            person.UpdatedAt = DateTime.UtcNow;
            return;
        }

        if (person.MedicareLookupAttemptedAt != null)
            return;

        var record = await _cmsClient.GetByNpiAsync(person.Npi, ct).ConfigureAwait(false);

        if (record == null)
        {
            person.MedicareLookupResult = "not_found";
        }
        else
        {
            person.MedicareLookupResult = "found";
            StoreUtilizationRecord(context, person.Id, record, _dataYear);
        }

        var services = await _cmsClient.GetServicesByNpiAsync(person.Npi, ct).ConfigureAwait(false);
        if (services.Count > 0)
        {
            StoreProcedureRecords(context, person.Id, services, _dataYear);
        }

        person.MedicareLookupAttemptedAt = DateTime.UtcNow;
        person.UpdatedAt = DateTime.UtcNow;
    }

    private static void StoreUtilizationRecord(ClinicalTrialsContext context, Guid personId, CmsMedicareRecord record, int dataYear)
    {
        var chronicConditions = new Dictionary<string, decimal?>();
        AddCond(chronicConditions, "ADHD_Conduct", record.BeneCcBhAdhdOthCdPct);
        AddCond(chronicConditions, "Alcohol_Drug", record.BeneCcBhAlcoholDrugPct);
        AddCond(chronicConditions, "Tobacco", record.BeneCcBhTobaccoPct);
        AddCond(chronicConditions, "Alzheimers_Dementia", record.BeneCcBhAlzNonAlzdemPct);
        AddCond(chronicConditions, "Anxiety", record.BeneCcBhAnxietyPct);
        AddCond(chronicConditions, "Bipolar", record.BeneCcBhBipolarPct);
        AddCond(chronicConditions, "Depression", record.BeneCcBhDepressPct);
        AddCond(chronicConditions, "PTSD", record.BeneCcBhPtsdPct);
        AddCond(chronicConditions, "Schizophrenia", record.BeneCcBhSchizoOthPsyPct);
        AddCond(chronicConditions, "Asthma", record.BeneCcPhAsthmaPct);
        AddCond(chronicConditions, "Atrial_Fibrillation", record.BeneCcPhAfibPct);
        AddCond(chronicConditions, "Cancer", record.BeneCcPhCancerPct);
        AddCond(chronicConditions, "CKD", record.BeneCcPhCkdPct);
        AddCond(chronicConditions, "COPD", record.BeneCcPhCopdPct);
        AddCond(chronicConditions, "Diabetes", record.BeneCcPhDiabetesPct);
        AddCond(chronicConditions, "Heart_Failure", record.BeneCcPhHfNonIhdPct);
        AddCond(chronicConditions, "Hyperlipidemia", record.BeneCcPhHyperlipidemiaPct);
        AddCond(chronicConditions, "Hypertension", record.BeneCcPhHypertensionPct);
        AddCond(chronicConditions, "Ischemic_Heart", record.BeneCcPhIschemicHeartPct);
        AddCond(chronicConditions, "Osteoporosis", record.BeneCcPhOsteoporosisPct);
        AddCond(chronicConditions, "Parkinsons", record.BeneCcPhParkinsonPct);
        AddCond(chronicConditions, "Arthritis", record.BeneCcPhArthritisPct);
        AddCond(chronicConditions, "Stroke_TIA", record.BeneCcPhStrokeTiaPct);

        var entity = new MedicareUtilizationEntity
        {
            InvestigatorPersonId = personId,
            DataYear = dataYear,
            ProviderType = record.ProviderType,
            TotalBeneficiaries = record.TotalBeneficiaries,
            TotalServices = record.TotalServices,
            TotalSubmittedCharges = record.TotalSubmittedCharges,
            TotalMedicareAllowedAmount = record.TotalMedicareAllowedAmount,
            TotalMedicarePaymentAmount = record.TotalMedicarePaymentAmount,
            TotalMedicareStandardizedAmount = record.TotalMedicareStandardizedAmount,
            MedicareParticipationIndicator = record.MedicareParticipationIndicator,
            BeneAgeLt65Count = record.BeneAgeLt65Count,
            BeneAge65To74Count = record.BeneAge65To74Count,
            BeneAge75To84Count = record.BeneAge75To84Count,
            BeneAgeGt84Count = record.BeneAgeGt84Count,
            BeneFemaleCount = record.BeneFemaleCount,
            BeneMaleCount = record.BeneMaleCount,
            BeneDualCount = record.BeneDualCount,
            BeneNonDualCount = record.BeneNonDualCount,
            ChronicConditionsJson = JsonSerializer.Serialize(chronicConditions),
            AvgRiskScore = record.AvgRiskScore,
            MedicalServices = record.MedicalServices,
            DrugServices = record.DrugServices,
            MedicalMedicarePayment = record.MedicalMedicarePayment,
            DrugMedicarePayment = record.DrugMedicarePayment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.MedicareUtilizations.Add(entity);
    }

    private static void StoreProcedureRecords(ClinicalTrialsContext context, Guid personId, IReadOnlyList<CmsMedicareServiceRecord> records, int dataYear)
    {
        var grouped = records
            .GroupBy(sr => new { sr.HcpcsCode, sr.PlaceOfService })
            .Select(g => new MedicareProcedureEntity
            {
                InvestigatorPersonId = personId,
                DataYear = dataYear,
                HcpcsCode = g.Key.HcpcsCode ?? string.Empty,
                HcpcsDescription = g.First().HcpcsDescription,
                PlaceOfService = g.Key.PlaceOfService,
                BeneficiaryCount = g.Sum(r => r.BeneficiaryCount ?? 0),
                ServiceCount = g.Sum(r => r.ServiceCount ?? 0),
                SubmittedChargeAmount = g.Average(r => r.SubmittedChargeAmount),
                MedicareAllowedAmount = g.Average(r => r.MedicareAllowedAmount),
                MedicarePaymentAmount = g.Average(r => r.MedicarePaymentAmount),
                ProviderType = g.First().ProviderType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        foreach (var entity in grouped)
        {
            context.MedicareProcedures.Add(entity);
        }
    }

    private static void AddCond(Dictionary<string, decimal?> dict, string key, decimal? value)
    {
        if (value.HasValue)
            dict[key] = value;
    }
}
