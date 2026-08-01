namespace Scrapers.Utilities
{
    /// <summary>
    /// Rule-based scorer for NPI candidate disambiguation.
    /// Computes a weighted match score per candidate (0..1) and applies the
    /// corroboration rule: a candidate is only auto-assigned when it is the
    /// unique best match above threshold AND an independent signal other than
    /// the name itself corroborates it (state, city, organization, specialty,
    /// license state, department, other name, or ORCID).
    /// ORCID is a hard identity override and always auto-assigns.
    /// </summary>
    internal static class NpiCandidateScorer
    {
        private const int ExactNameWeight = 40;
        private const int MiddleNameWeight = 15;
        private const int CredentialWeight = 5;
        private const int StateWeight = 10;
        private const int CityWeight = 5;
        private const int OrgWeight = 10;
        private const int OtherNameWeight = 5;
        private const int SpecialtyWeight = 15;
        private const int LicenseStateWeight = 5;
        private const int DepartmentWeight = 5;

        public const double AssignThreshold = 0.6;
        public const double SingleCandidateThreshold = 0.5;

        public enum NpiResolutionOutcome
        {
            Assigned,
            Ambiguous,
            NotFound
        }

        public sealed record NpiResolution(NpiResolutionOutcome Outcome, string? AssignedNumber, double? AssignedScore);

        /// <summary>
        /// Computes the weighted match score (0..1) for a candidate.
        /// Deactivated candidates always score 0. Missing data on either side
        /// is excluded from the denominator rather than voting against the candidate.
        /// </summary>
        public static double Score(NpiCandidateFeatures features)
        {
            if (features.IsDeactivated)
            {
                return 0;
            }

            int applicable = 0;
            int matched = 0;

            if (features.HasPersonName)
            {
                applicable += ExactNameWeight;
                if (features.ExactNameMatch)
                {
                    matched += ExactNameWeight;
                }
            }

            Add(features.MiddleNameMatch, MiddleNameWeight);
            Add(features.CredentialMatch, CredentialWeight);
            Add(features.StateMatch, StateWeight);
            Add(features.CityMatch, CityWeight);
            Add(features.OrgMatch, OrgWeight);
            Add(features.OtherNameMatch, OtherNameWeight);
            Add(features.SpecialtyMatch, SpecialtyWeight);
            Add(features.LicenseStateMatch, LicenseStateWeight);
            Add(features.DepartmentMatch, DepartmentWeight);

            return applicable == 0 ? 0 : (double)matched / applicable;

            void Add(bool? feature, int weight)
            {
                if (feature is not null)
                {
                    applicable += weight;
                    if (feature.Value)
                    {
                        matched += weight;
                    }
                }
            }
        }

        /// <summary>
        /// Resolves the candidate list to an NPI decision. Sets <c>Score</c> on every
        /// candidate as a side effect so callers can persist it.
        /// </summary>
        public static NpiResolution Resolve(
            IReadOnlyList<NpiCandidateFeatures> candidates,
            double assignThreshold = AssignThreshold,
            double singleCandidateThreshold = SingleCandidateThreshold)
        {
            if (candidates.Count == 0)
            {
                return new NpiResolution(NpiResolutionOutcome.NotFound, null, null);
            }

            foreach (var candidate in candidates)
            {
                candidate.Score = Score(candidate);
            }

            var active = candidates.Where(c => !c.IsDeactivated).ToList();
            if (active.Count == 0)
            {
                return new NpiResolution(NpiResolutionOutcome.Ambiguous, null, null);
            }

            var orcidMatches = active.Where(c => c.OrcidMatch).ToList();
            if (orcidMatches.Count == 1)
            {
                var orcidWinner = orcidMatches[0];
                orcidWinner.Score = Math.Max(orcidWinner.Score ?? 0, 1.0);
                return new NpiResolution(NpiResolutionOutcome.Assigned, orcidWinner.Number, orcidWinner.Score);
            }

            if (orcidMatches.Count > 1)
            {
                return new NpiResolution(NpiResolutionOutcome.Ambiguous, null, null);
            }

            var ranked = active.OrderByDescending(c => c.Score ?? 0).ToList();
            var topScore = ranked[0].Score ?? 0;
            var uniqueBest = ranked.Count == 1 || (ranked[1].Score ?? 0) < topScore - 1e-9;

            if (!uniqueBest)
            {
                return new NpiResolution(NpiResolutionOutcome.Ambiguous, null, null);
            }

            var winner = ranked[0];
            var threshold = active.Count == 1 ? singleCandidateThreshold : assignThreshold;
            bool corroborated = HasCorroboration(winner);

            if ((winner.Score ?? 0) >= threshold && (active.Count == 1 || corroborated))
            {
                return new NpiResolution(NpiResolutionOutcome.Assigned, winner.Number, winner.Score);
            }

            return new NpiResolution(NpiResolutionOutcome.Ambiguous, null, null);
        }

        private static bool HasCorroboration(NpiCandidateFeatures features) =>
            features.StateMatch == true
            || features.CityMatch == true
            || features.OrgMatch == true
            || features.OtherNameMatch == true
            || features.SpecialtyMatch == true
            || features.LicenseStateMatch == true
            || features.DepartmentMatch == true
            || features.OrcidMatch == true;
    }
}
