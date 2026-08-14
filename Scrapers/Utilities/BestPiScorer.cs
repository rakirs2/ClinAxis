using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
    /// <summary>
    /// P6 "Best-PI" rule-based scorer (issue #170 Phase 1, MVP pillar P6). Pure and DB-free:
    /// ranks investigator candidates for a (therapy, condition, population, region,
    /// enrollment-target) request with explainable 0..1 factor scores and a weighted total.
    /// The service layer (POST /api/recommend/investigators) feeds it candidate signals and
    /// requested specialty categories; this class has no data access.
    ///
    /// Scoring model (documented decisions, issue #170 §Phase 1):
    /// - Factor weights: experience 0.15, completion 0.30, condition fit 0.25, velocity 0.10,
    ///   publication 0.15, geographic 0.05. Total = weighted mean over the factors that are
    ///   present; missing factors abstain and the surviving weights renormalize (the
    ///   NpiCandidateScorer/RecEngineSignals abstain idiom — missing data never drags a score
    ///   toward zero).
    /// - Experience: min(1, studyCount/20).
    /// - Completion: the 0..1 completion rate from RecEngineSignals directly.
    /// - Condition fit: |requested ∩ candidate categories| / |requested|; no requested
    ///   categories (request with no condition/therapy) abstains.
    /// - Velocity: patients/month. With an enrollment target, the expectation is
    ///   target/36 months (3-year trial); without one, a flat 20/month cap.
    /// - Publication: 0.6 * min(1, hIndex/50) + 0.4 * min(1, paperCount/200) over whichever
    ///   sub-signals exist.
    /// - Geographic: exact normalized country-name match (ordinal-ignore-case) against the
    ///   regions the candidate's studies cover; no requested region or no candidate region
    ///   data abstains. Broad regions (EU) and proximity are deferred.
    /// - Population: accepted on the request for API stability but contributes no signal in
    ///   v1 — per-investigator population data (eligibility parsing) is phase-2 work.
    /// </summary>
    internal static class BestPiScorer
    {
        public const double ExperienceWeight = 0.15;
        public const double CompletionWeight = 0.30;
        public const double ConditionFitWeight = 0.25;
        public const double VelocityWeight = 0.10;
        public const double PublicationWeight = 0.15;
        public const double GeographicWeight = 0.05;

        private const double StudyCountCap = 20;
        private const double HIndexCap = 50;
        private const double PaperCountCap = 200;
        private const double DefaultVelocityCapPerMonth = 20;
        private const double TrialDurationMonths = 36;

        /// <summary>The recommendation request; category derivation happens service-side.</summary>
        public readonly record struct RecommendationRequest(
            IReadOnlyCollection<string> RequestedCategories,
            string? Region,
            int? EnrollmentTarget,
            string? Population);

        /// <summary>Per-candidate signals the service gathers (query-time, see RecEngineSignals).</summary>
        public readonly record struct CandidateSignals(
            Guid Uuid,
            string? Name,
            string? PrimaryAffiliation,
            int StudyCount,
            double? CompletionRate,
            double? EnrollmentVelocity,
            int? HIndex,
            int? PaperCount,
            IReadOnlyCollection<string> ConditionCategories,
            IReadOnlyCollection<string> Regions);

        /// <summary>Explainable 0..1 factor scores; null = factor abstained (no data / not requested).</summary>
        public readonly record struct FactorScores(
            double? Experience,
            double? Completion,
            double? ConditionFit,
            double? Velocity,
            double? Publication,
            double? Geographic);

        public readonly record struct Recommendation(
            CandidateSignals Candidate,
            double Score,
            FactorScores Factors);

        public static double ExperienceScore(int studyCount)
        {
            return Math.Min(1.0, studyCount / (double)StudyCountCap);
        }

        public static double? CompletionScore(double? completionRate)
        {
            return completionRate is not null ? Math.Clamp(completionRate.Value, 0.0, 1.0) : null;
        }

        public static double? ConditionFitScore(
            IReadOnlyCollection<string> requestedCategories,
            IReadOnlyCollection<string> candidateCategories)
        {
            if (requestedCategories.Count == 0)
            {
                return null;
            }

            var requested = requestedCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requested.Count == 0)
            {
                return null;
            }

            var matched = candidateCategories.Count(c => requested.Contains(c));
            return matched / (double)requested.Count;
        }

        public static double? VelocityScore(double? enrollmentVelocity, int? enrollmentTarget)
        {
            if (enrollmentVelocity is null)
            {
                return null;
            }

            var cap = enrollmentTarget is > 0
                ? enrollmentTarget.Value / TrialDurationMonths
                : DefaultVelocityCapPerMonth;

            return Math.Min(1.0, enrollmentVelocity.Value / cap);
        }

        public static double? PublicationScore(int? hIndex, int? paperCount)
        {
            double numerator = 0;
            double weightSum = 0;

            if (hIndex is not null)
            {
                numerator += 0.6 * Math.Min(1.0, hIndex.Value / (double)HIndexCap);
                weightSum += 0.6;
            }

            if (paperCount is not null)
            {
                numerator += 0.4 * Math.Min(1.0, paperCount.Value / (double)PaperCountCap);
                weightSum += 0.4;
            }

            return weightSum > 0 ? numerator / weightSum : null;
        }

        public static double? GeographicScore(string? requestedRegion, IReadOnlyCollection<string> candidateRegions)
        {
            if (string.IsNullOrWhiteSpace(requestedRegion) || candidateRegions.Count == 0)
            {
                return null;
            }

            return candidateRegions.Any(r => string.Equals(r, requestedRegion, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0;
        }

        public static Recommendation Score(CandidateSignals candidate, RecommendationRequest request)
        {
            var experience = ExperienceScore(candidate.StudyCount);
            var completion = CompletionScore(candidate.CompletionRate);
            var conditionFit = ConditionFitScore(request.RequestedCategories, candidate.ConditionCategories);
            var velocity = VelocityScore(candidate.EnrollmentVelocity, request.EnrollmentTarget);
            var publication = PublicationScore(candidate.HIndex, candidate.PaperCount);
            var geographic = GeographicScore(request.Region, candidate.Regions);

            var factors = new FactorScores(experience, completion, conditionFit, velocity, publication, geographic);

            double numerator = 0;
            double weightSum = 0;

            numerator += ExperienceWeight * experience;
            weightSum += ExperienceWeight;

            if (completion is not null)
            {
                numerator += CompletionWeight * completion.Value;
                weightSum += CompletionWeight;
            }

            if (conditionFit is not null)
            {
                numerator += ConditionFitWeight * conditionFit.Value;
                weightSum += ConditionFitWeight;
            }

            if (velocity is not null)
            {
                numerator += VelocityWeight * velocity.Value;
                weightSum += VelocityWeight;
            }

            if (publication is not null)
            {
                numerator += PublicationWeight * publication.Value;
                weightSum += PublicationWeight;
            }

            if (geographic is not null)
            {
                numerator += GeographicWeight * geographic.Value;
                weightSum += GeographicWeight;
            }

            var score = weightSum > 0 ? numerator / weightSum : 0.0;
            return new Recommendation(candidate, score, factors);
        }

        public static IReadOnlyList<Recommendation> Rank(
            IEnumerable<CandidateSignals> candidates,
            RecommendationRequest request)
        {
            ArgumentNullException.ThrowIfNull(candidates);

            return candidates
                .Select(c => Score(c, request))
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.Candidate.StudyCount)
                .ThenBy(r => r.Candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
