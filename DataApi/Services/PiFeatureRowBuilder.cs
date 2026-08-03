using System.Globalization;

namespace DataApi.Services;

/// <summary>
/// Pure per-row assembly for the PI completion-model training CSV. All inputs
/// are plain data pulled by PiFeaturesExportEndpoints' EF queries; this class
/// performs no I/O so every row computation is unit-testable.
/// </summary>
internal static class PiFeatureRowBuilder
{
    public const string Found = "found";

    public static readonly string[] Header =
    [
        "study_nct_id", "person_id", "full_name", "overall_status", "label",
        "prior_study_count", "prior_completed_count", "prior_enrollment_total", "prior_completion_rate",
        "papers_before_start", "papers_per_year_before_start",
        "medicare_beneficiaries", "medicare_services", "medicare_payments", "medicare_risk_score", "has_medicare",
        "research_payments", "general_payments", "payor_count", "has_payments",
        "current_h_index", "citation_count", "i10_index", "total_papers", "has_metrics",
    ];

    public static string[] BuildRow(
        TargetRow target,
        HistoryFeatures history,
        IReadOnlyList<DateOnly> papers,
        IReadOnlyList<MedicareRow> medicare,
        IReadOnlyList<OpenPaymentRow> openPayments,
        MetricsRow? metrics)
    {
        var label = target.OverallStatus == "COMPLETED" ? 1 : 0;

        var papersBefore = papers.Count(p => p < target.StartDate);
        var firstPaperYear = papers.Count > 0 ? papers[0].Year : (int?)null;
        var papersPerYear = papersBefore > 0 && firstPaperYear.HasValue
            ? (double)papersBefore / Math.Max(1, target.StartDate.Year - firstPaperYear.Value + 1)
            : 0.0;

        var medicareRows = medicare.Where(m => m.DataYear <= target.StartDate.Year).ToList();
        var hasMedicare = medicareRows.Count > 0;
        var beneficiaries = medicareRows.Sum(m => (long?)m.TotalBeneficiaries) ?? 0L;
        var services = medicareRows.Sum(m => m.TotalServices) ?? 0L;
        var payments = medicareRows.Sum(m => (double?)m.TotalMedicarePaymentAmount) ?? 0.0;
        var risks = medicareRows.Where(m => m.AvgRiskScore.HasValue).Select(m => (double)m.AvgRiskScore!.Value).ToList();
        var riskScore = risks.Count > 0 ? risks.Average() : 0.0;

        var paymentRows = openPayments.Where(o => o.DataYear <= target.StartDate.Year).ToList();
        var hasPayments = paymentRows.Count > 0;
        var research = paymentRows
            .Where(o => o.PaymentType == "research")
            .Sum(o => (double?)o.PaymentAmount) ?? 0.0;
        var general = paymentRows
            .Where(o => o.PaymentType != "research")
            .Sum(o => (double?)o.PaymentAmount) ?? 0.0;
        var payorCount = paymentRows
            .Select(o => o.PayorName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .Count();

        var hasMetrics = metrics?.LookupResult == Found;

        return
        [
            target.StudyNctId,
            target.PersonId.ToString(),
            Quote(target.FullName ?? string.Empty),
            target.OverallStatus,
            label.ToString(CultureInfo.InvariantCulture),
            history.StudyCount.ToString(CultureInfo.InvariantCulture),
            history.CompletedCount.ToString(CultureInfo.InvariantCulture),
            history.EnrollmentTotal.ToString(CultureInfo.InvariantCulture),
            Fmt(history.CompletionRate),
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
            (metrics?.HIndex ?? 0).ToString(CultureInfo.InvariantCulture),
            (metrics?.CitationCount ?? 0).ToString(CultureInfo.InvariantCulture),
            (metrics?.I10Index ?? 0).ToString(CultureInfo.InvariantCulture),
            (metrics?.TotalPapers ?? 0).ToString(CultureInfo.InvariantCulture),
            Bool(hasMetrics),
        ];
    }

    /// <summary>
    /// Aggregates a person's prior-study rows (one row per investigator-study
    /// role assignment) into the history features: distinct study count,
    /// completed count, summed max enrollment, and completion rate.
    /// </summary>
    public static HistoryFeatures ComputeHistoryFeatures(IEnumerable<HistoryStudyRow> studies)
    {
        var grouped = studies
            .GroupBy(s => s.StudyNctId)
            .Select(g => new
            {
                Completed = g.Any(x => x.OverallStatus == "COMPLETED"),
                Enrollment = g.Max(x => x.EnrollmentCount) ?? 0
            })
            .ToList();
        var studyCount = grouped.Count;
        var completed = grouped.Count(s => s.Completed);
        var enrollmentTotal = grouped.Sum(s => s.Enrollment);
        var completionRate = studyCount > 0 ? (double)completed / studyCount : 0.0;
        return new HistoryFeatures(studyCount, completed, enrollmentTotal, completionRate);
    }

    public static string Quote(string value)
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
}

internal sealed record TargetRow(string StudyNctId, Guid PersonId, string? FullName, DateOnly StartDate, string OverallStatus, int? EnrollmentCount);

internal sealed record HistoryFeatures(int StudyCount, int CompletedCount, long EnrollmentTotal, double CompletionRate)
{
    public static HistoryFeatures Empty { get; } = new(0, 0, 0, 0.0);
}

internal sealed record HistoryStudyRow(string StudyNctId, string? OverallStatus, int? EnrollmentCount);

internal sealed record MedicareRow(int DataYear, int? TotalBeneficiaries, long? TotalServices, decimal? TotalMedicarePaymentAmount, decimal? AvgRiskScore);

internal sealed record OpenPaymentRow(int DataYear, string PaymentType, decimal? PaymentAmount, string? PayorName);

internal sealed record MetricsRow(int? HIndex, int? CitationCount, int? I10Index, int? TotalPapers, string? LookupResult);
