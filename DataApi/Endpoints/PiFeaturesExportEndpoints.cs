using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using DataApi.Services;

namespace DataApi.Endpoints;

/// <summary>
/// Exports the PI completion-model training frame as CSV, one row per
/// (window study x principal investigator). Feature semantics mirror the v0
/// training pipeline (experiments/pi-completion-model/features.py), with the
/// identity key upgraded from normalized name to person id.
///
/// Column groups:
///   base         prior trial history strictly before the window start
///   pubmed       papers published strictly before the study's start date
///   medicare     CMS Medicare utilization with DataYear &lt;= study start year
///   openpay      CMS Open Payments (Sunshine Act) with DataYear &lt;= study start year
///   semach       Semantic Scholar metrics — CURRENT snapshot, leaky by design;
///                diagnostic only, never a deployed feature without as-of rebuild
///
/// Sources with no rows for a person are exported as explicit has_* = 0 flags,
/// never silently imputed.
/// </summary>
internal static class PiFeaturesExportEndpoints
{
    private const int HistoryYearMin = 2000;
    private const string DateFormat = "yyyy-MM-dd";
    private const string PrincipalInvestigatorRole = "PRINCIPAL_INVESTIGATOR";
    private const string SemanticScholarSource = "SemanticScholar";

    internal static void MapPiFeaturesExportEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/export/training/pi-features", async (HttpResponse response, string? from, string? to) =>
        {
            var validationError = ValidateWindow(from, to, out var windowStart, out var windowEnd);
            if (validationError is not null)
            {
                response.StatusCode = 400;
                await response.WriteAsJsonAsync(new { error = validationError });
                return;
            }

            var historyStart = new DateOnly(HistoryYearMin, 1, 1);

            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            var targets = await (
                from si in ctx.StudyInvestigators
                join st in ctx.Studies on si.StudyNctId equals st.NctId
                join p in ctx.InvestigatorPersons on si.InvestigatorPersonId equals p.Id
                where si.RoleOnStudy == PrincipalInvestigatorRole
                      && st.StartDate >= windowStart
                      && st.StartDate <= windowEnd
                      && (st.OverallStatus == "COMPLETED" || st.OverallStatus == "TERMINATED")
                select new TargetRow(
                    si.StudyNctId,
                    p.Id,
                    p.FullName,
                    st.StartDate!.Value,
                    st.OverallStatus!,
                    st.EnrollmentCount))
                .AsNoTracking()
                .ToListAsync();

            targets = targets.GroupBy(t => (t.StudyNctId, t.PersonId)).Select(g => g.First()).ToList();

            var personIds = targets.Select(t => t.PersonId).Distinct().ToList();

            var history = await (
                from si in ctx.StudyInvestigators
                join st in ctx.Studies on si.StudyNctId equals st.NctId
                where st.StartDate >= historyStart && st.StartDate < windowStart
                select new { si.InvestigatorPersonId, si.StudyNctId, st.OverallStatus, st.EnrollmentCount })
                .AsNoTracking()
                .ToListAsync();

            var historyByPerson = history
                .GroupBy(h => h.InvestigatorPersonId)
                .ToDictionary(
                    g => g.Key,
                    g => PiFeatureRowBuilder.ComputeHistoryFeatures(
                        g.Select(x => new HistoryStudyRow(x.StudyNctId, x.OverallStatus, x.EnrollmentCount))));

            var papersByPerson = (await (
                from ip in ctx.InvestigatorPapers
                join pp in ctx.PubmedPapers on ip.PubmedPaperId equals pp.Id
                where personIds.Contains(ip.InvestigatorPersonId) && pp.PublicationDate != null
                select new { ip.InvestigatorPersonId, Date = DateOnly.FromDateTime(pp.PublicationDate!.Value) })
                .AsNoTracking()
                .ToListAsync())
                .GroupBy(p => p.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Date).OrderBy(d => d).ToList());

            var medicareByPerson = (await ctx.MedicareUtilizations
                .Where(m => personIds.Contains(m.InvestigatorPersonId))
                .Select(m => new
                {
                    m.InvestigatorPersonId,
                    m.DataYear,
                    m.TotalBeneficiaries,
                    m.TotalServices,
                    m.TotalMedicarePaymentAmount,
                    m.AvgRiskScore
                })
                .AsNoTracking()
                .ToListAsync())
                .GroupBy(m => m.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g
                    .Select(m => new MedicareRow(
                        m.DataYear, m.TotalBeneficiaries, m.TotalServices, m.TotalMedicarePaymentAmount, m.AvgRiskScore))
                    .ToList());

            var openPaymentsByPerson = (await ctx.OpenPayments
                .Where(o => personIds.Contains(o.InvestigatorPersonId))
                .Select(o => new { o.InvestigatorPersonId, o.DataYear, o.PaymentType, o.PaymentAmount, o.PayorName })
                .AsNoTracking()
                .ToListAsync())
                .GroupBy(o => o.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g
                    .Select(o => new OpenPaymentRow(o.DataYear, o.PaymentType, o.PaymentAmount, o.PayorName))
                    .ToList());

            var metricsByPerson = (await ctx.InvestigatorMetrics
                .Where(m => personIds.Contains(m.InvestigatorPersonId) && m.Source == SemanticScholarSource)
                .Select(m => new { m.InvestigatorPersonId, m.HIndex, m.CitationCount, m.I10Index, m.TotalPapers, m.LookupResult })
                .AsNoTracking()
                .ToListAsync())
                .GroupBy(m => m.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g
                    .Select(m => new MetricsRow(m.HIndex, m.CitationCount, m.I10Index, m.TotalPapers, m.LookupResult))
                    .First());

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"pi-features.csv\"";
            await response.WriteAsync(string.Join(",", PiFeatureRowBuilder.Header) + "\n");

            foreach (var t in targets)
            {
                var cols = PiFeatureRowBuilder.BuildRow(
                    t,
                    historyByPerson.GetValueOrDefault(t.PersonId, HistoryFeatures.Empty),
                    papersByPerson.GetValueOrDefault(t.PersonId, []),
                    medicareByPerson.GetValueOrDefault(t.PersonId, []),
                    openPaymentsByPerson.GetValueOrDefault(t.PersonId, []),
                    metricsByPerson.GetValueOrDefault(t.PersonId));
                await response.WriteAsync(string.Join(",", cols) + "\n");
            }
        });
    }

    /// <summary>
    /// Validates the client-supplied training window. Both bounds are required
    /// (the client owns the window — no defaults), must parse as yyyy-MM-dd,
    /// must lie within [2000-01-01, today], and from must not be after to.
    /// Returns an error message or null when the window is valid.
    /// </summary>
    internal static string? ValidateWindow(string? from, string? to, out DateOnly windowStart, out DateOnly windowEnd)
    {
        windowStart = default;
        windowEnd = default;

        if (string.IsNullOrWhiteSpace(from))
        {
            return "Missing required query parameter 'from' (expected yyyy-MM-dd).";
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            return "Missing required query parameter 'to' (expected yyyy-MM-dd).";
        }

        if (!DateOnly.TryParseExact(from, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out windowStart))
        {
            return $"Query parameter 'from' must be a valid date in yyyy-MM-dd format, got '{from}'.";
        }

        if (!DateOnly.TryParseExact(to, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out windowEnd))
        {
            return $"Query parameter 'to' must be a valid date in yyyy-MM-dd format, got '{to}'.";
        }

        var minDate = new DateOnly(HistoryYearMin, 1, 1);
        if (windowStart < minDate)
        {
            return $"Window start must not be before {minDate:yyyy-MM-dd} (prior-history data starts then), got '{from}'.";
        }

        var maxDate = DateOnly.FromDateTime(DateTime.Today);
        if (windowEnd > maxDate)
        {
            return $"Window end must not be after today ({maxDate:yyyy-MM-dd}), got '{to}'.";
        }

        if (windowStart > windowEnd)
        {
            return $"Window start '{from}' must not be after window end '{to}'.";
        }

        return null;
    }
}
