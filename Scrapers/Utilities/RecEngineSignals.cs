using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Rec-engine signal calculations (issue #381, MVP pillar P6). Pure and DB-free so the
    /// whole signal pipeline is unit-testable: per-study completion signal from CT.gov
    /// overall_status, per-study enrollment velocity, and the per-investigator weighted
    /// rollup consumed by the P6 rule-based scorer.
    ///
    /// Documented decisions (issue #381 §3):
    /// - Completion signal: 0..1 scale of how far a study progressed toward completion.
    ///   COMPLETED=1.0, ACTIVE_NOT_RECRUITING=0.5, SUSPENDED=0.3, ENROLLING_BY_INVITATION/
    ///   RECRUITING/ACTIVE=0.2, NOT_YET_RECRUITING=0.0, TERMINATED=0.0, WITHDRAWN=0.0.
    ///   UNKNOWN and null abstain (excluded from the mean, never penalized).
    /// - Enrollment velocity: enrollment / active months, where active months run from
    ///   startDate to (completionDate ?? asOf). Missing enrollment or startDate abstains.
    /// - Rollup: weighted mean with weight = (enrollment ?? 1) * recencyFactor, where
    ///   recencyFactor = 1 / (1 + years since the study's end). Bigger, more recent
    ///   studies dominate; missing data abstains instead of pulling toward zero.
    /// - Query-time computation (no persisted columns): signals derive entirely from
    ///   existing scalar columns; persisted materialization can come later if the P6
    ///   scorer benchmarks justify it.
    /// </summary>
    internal static class RecEngineSignals
    {
        /// <summary>Minimal per-study shape the rollup needs; keeps the aggregator DB-free.</summary>
        public readonly record struct StudySignalInput(
            string? OverallStatus,
            int? EnrollmentCount,
            DateOnly? StartDate,
            DateOnly? CompletionDate);

        /// <summary>Aggregated per-investigator signals.</summary>
        public readonly record struct AggregateSignals(double? CompletionRate, double? EnrollmentVelocity, int IncludedStudyCount);

        private const double DaysPerMonth = 30.44;

        public static double? CompletionSignalFromStatus(string? overallStatus)
        {
            return overallStatus switch
            {
                "COMPLETED" => 1.0,
                "ACTIVE_NOT_RECRUITING" => 0.5,
                "SUSPENDED" => 0.3,
                "ENROLLING_BY_INVITATION" => 0.2,
                "RECRUITING" => 0.2,
                "ACTIVE" => 0.2,
                "NOT_YET_RECRUITING" => 0.0,
                "TERMINATED" => 0.0,
                "WITHDRAWN" => 0.0,
                _ => null
            };
        }

        public static double? EnrollmentVelocity(int? enrollment, DateOnly? startDate, DateOnly? completionDate, DateOnly asOf)
        {
            if (enrollment is not ( > 0) || startDate is null)
            {
                return null;
            }

            var end = completionDate ?? asOf;
            var activeMonths = (end.DayNumber - startDate.Value.DayNumber) / DaysPerMonth;
            if (activeMonths <= 0)
            {
                return null;
            }

            return enrollment.Value / activeMonths;
        }

        public static AggregateSignals Aggregate(IEnumerable<StudySignalInput> studies, DateOnly asOf)
        {
            ArgumentNullException.ThrowIfNull(studies);

            double completionWeightSum = 0;
            double velocityWeightSum = 0;
            double completionNumerator = 0;
            double velocityNumerator = 0;
            int included = 0;

            foreach (var study in studies)
            {
                var completion = CompletionSignalFromStatus(study.OverallStatus);
                var velocity = EnrollmentVelocity(study.EnrollmentCount, study.StartDate, study.CompletionDate, asOf);
                if (completion is null && velocity is null)
                {
                    continue;
                }

                var endDate = study.CompletionDate ?? study.StartDate ?? asOf;
                var yearsSinceEnd = Math.Max(0, (asOf.DayNumber - endDate.DayNumber) / 365.25);
                var weight = (study.EnrollmentCount ?? 1) * (1.0 / (1.0 + yearsSinceEnd));

                if (completion is not null)
                {
                    completionNumerator += weight * completion.Value;
                    completionWeightSum += weight;
                }

                if (velocity is not null)
                {
                    velocityNumerator += weight * velocity.Value;
                    velocityWeightSum += weight;
                }

                included++;
            }

            double? completionRate = completionWeightSum > 0 ? completionNumerator / completionWeightSum : null;
            double? velocityResult = velocityWeightSum > 0 ? velocityNumerator / velocityWeightSum : null;
            return new AggregateSignals(completionRate, velocityResult, included);
        }
    }
}
