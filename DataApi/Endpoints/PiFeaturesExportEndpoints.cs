using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;

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
    private const string Found = "found";

    private static readonly string[] Header =
    [
        "study_nct_id", "person_id", "full_name", "overall_status", "label",
        "prior_study_count", "prior_completed_count", "prior_enrollment_total", "prior_completion_rate",
        "papers_before_start", "papers_per_year_before_start",
        "medicare_beneficiaries", "medicare_services", "medicare_payments", "medicare_risk_score", "has_medicare",
        "research_payments", "general_payments", "payor_count", "has_payments",
        "current_h_index", "citation_count", "i10_index", "total_papers", "has_metrics",
    ];

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
                    g =>
                    {
                        var studies = g
                            .GroupBy(s => s.StudyNctId)
                            .Select(s => new
                            {
                                s.Key,
                                Completed = s.Any(x => x.OverallStatus == "COMPLETED"),
                                Enrollment = s.Max(x => x.EnrollmentCount) ?? 0
                            })
                            .ToList();
                        var studyCount = studies.Count;
                        var completed = studies.Count(s => s.Completed);
                        var enrollmentTotal = studies.Sum(s => s.Enrollment);
                        var completionRate = studyCount > 0 ? (double)completed / studyCount : 0.0;
                        return new HistoryFeatures(studyCount, completed, enrollmentTotal, completionRate);
                    });

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
                .ToDictionary(g => g.Key, g => g.ToList());

            var openPaymentsByPerson = (await ctx.OpenPayments
                .Where(o => personIds.Contains(o.InvestigatorPersonId))
                .Select(o => new { o.InvestigatorPersonId, o.DataYear, o.PaymentType, o.PaymentAmount, o.PayorName })
                .AsNoTracking()
                .ToListAsync())
                .GroupBy(o => o.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var metricsByPerson = (await ctx.InvestigatorMetrics
                .Where(m => personIds.Contains(m.InvestigatorPersonId) && m.Source == SemanticScholarSource)
                .Select(m => new { m.InvestigatorPersonId, m.HIndex, m.CitationCount, m.I10Index, m.TotalPapers, m.LookupResult })
                .AsNoTracking()
                .ToListAsync())
                .GroupBy(m => m.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g.First());

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"pi-features.csv\"";
            await response.WriteAsync(string.Join(",", Header) + "\n");

            foreach (var t in targets)
            {
                var h = historyByPerson.GetValueOrDefault(t.PersonId, HistoryFeatures.Empty);
                var label = t.OverallStatus == "COMPLETED" ? 1 : 0;

                var papers = papersByPerson.GetValueOrDefault(t.PersonId, []);
                var papersBefore = papers.Count(p => p < t.StartDate);
                var firstPaperYear = papers.Count > 0 ? papers[0].Year : (int?)null;
                var papersPerYear = papersBefore > 0 && firstPaperYear.HasValue
                    ? (double)papersBefore / Math.Max(1, t.StartDate.Year - firstPaperYear.Value + 1)
                    : 0.0;

                var medicare = medicareByPerson.GetValueOrDefault(t.PersonId, []);
                var mRows = medicare.Where(m => m.DataYear <= t.StartDate.Year).ToList();
                var hasMedicare = mRows.Count > 0;
                var beneficiaries = mRows.Sum(m => (long?)m.TotalBeneficiaries) ?? 0L;
                var services = mRows.Sum(m => m.TotalServices) ?? 0L;
                var payments = mRows.Sum(m => (double?)m.TotalMedicarePaymentAmount) ?? 0.0;
                var risks = mRows.Where(m => m.AvgRiskScore.HasValue).Select(m => (double)m.AvgRiskScore!.Value).ToList();
                var riskScore = risks.Count > 0 ? risks.Average() : 0.0;

                var paymentsRows = openPaymentsByPerson.GetValueOrDefault(t.PersonId, [])
                    .Where(o => o.DataYear <= t.StartDate.Year)
                    .ToList();
                var hasPayments = paymentsRows.Count > 0;
                var research = paymentsRows
                    .Where(o => o.PaymentType == "research")
                    .Sum(o => (double?)o.PaymentAmount) ?? 0.0;
                var general = paymentsRows
                    .Where(o => o.PaymentType != "research")
                    .Sum(o => (double?)o.PaymentAmount) ?? 0.0;
                var payorCount = paymentsRows
                    .Select(o => o.PayorName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .Count();

                var metric = metricsByPerson.GetValueOrDefault(t.PersonId);
                var hasMetrics = metric?.LookupResult == Found;

                var cols = new string[]
                {
                    t.StudyNctId,
                    t.PersonId.ToString(),
                    Quote(t.FullName ?? string.Empty),
                    t.OverallStatus,
                    label.ToString(CultureInfo.InvariantCulture),
                    h.StudyCount.ToString(CultureInfo.InvariantCulture),
                    h.CompletedCount.ToString(CultureInfo.InvariantCulture),
                    h.EnrollmentTotal.ToString(CultureInfo.InvariantCulture),
                    Fmt(h.CompletionRate),
                    papersBefore.ToString(CultureInfo.InvariantCulture),
                    Fmt(papersPerYear),
                    beneficiaries.ToString(CultureInfo.InvariantCulture),
                    services.ToString(CultureInfo.InvariantCulture),
                    Fmt(payments),
                    Fmt(riskScore),
                    Bool(hasMedicare),
                    Fmt(research),
                    Fmt(general),
                    payorCount.ToString(CultureInfo.InvariantCulture),
                    Bool(hasPayments),
                    (metric?.HIndex ?? 0).ToString(CultureInfo.InvariantCulture),
                    (metric?.CitationCount ?? 0).ToString(CultureInfo.InvariantCulture),
                    (metric?.I10Index ?? 0).ToString(CultureInfo.InvariantCulture),
                    (metric?.TotalPapers ?? 0).ToString(CultureInfo.InvariantCulture),
                    Bool(hasMetrics),
                };
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

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string Fmt(double value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Bool(bool value)
    {
        return value ? "1" : "0";
    }

    private sealed record TargetRow(string StudyNctId, Guid PersonId, string? FullName, DateOnly StartDate, string OverallStatus, int? EnrollmentCount);

    private sealed record HistoryFeatures(int StudyCount, int CompletedCount, long EnrollmentTotal, double CompletionRate)
    {
        public static HistoryFeatures Empty { get; } = new(0, 0, 0, 0.0);
    }
}
