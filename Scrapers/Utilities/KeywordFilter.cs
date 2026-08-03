using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
    internal static class KeywordFilter
    {
        private static readonly HashSet<string> KnownShortMedicalTerms = new(StringComparer.OrdinalIgnoreCase)
        {
            "HIV", "HPV", "ALS", "MS", "IBS", "COPD", "ICU", "GI",
            "ENT", "CT", "MRI", "PET", "CVD", "CHF", "CAD", "CKD",
            "UTI", "STD", "PTSD", "ADHD", "GERD", "RA", "SLE",
            "NASH", "NAFLD", "OSA", "PCOS", "TBI", "SCI",
        };

        /// <summary>
        /// Valid trial-design descriptors (issue #343). These bypass the junk
        /// blocklist: they are legitimate study metadata even though they are
        /// not diseases and often not MeSH descriptors.
        /// </summary>
        private static readonly HashSet<string> DesignDescriptorAllowlist = new(StringComparer.OrdinalIgnoreCase)
        {
            "RANDOMIZED CONTROLLED TRIAL", "RANDOMIZED CLINICAL TRIAL",
            "RANDOMIZED CONTROLLED STUDY", "RANDOMISED CONTROLLED TRIAL",
            "RANDOMISED CLINICAL TRIAL", "CLUSTER RANDOMIZED CONTROLLED TRIAL",
            "OBSERVATIONAL STUDY", "INTERVENTIONAL STUDY", "CLINICAL TRIAL",
            "PILOT STUDY", "CASE-CONTROL", "CROSS-SECTIONAL STUDY",
            "PROSPECTIVE STUDY", "RETROSPECTIVE STUDY", "COHORT STUDY",
            "LONGITUDINAL STUDY", "CONTROLLED CLINICAL TRIAL",
            "PHASE 1", "PHASE I", "PHASE 2", "PHASE II",
            "PHASE 3", "PHASE III", "PHASE 4", "PHASE IV",
            "OPEN LABEL", "OPEN-LABEL", "DOUBLE BLIND", "DOUBLE-BLIND",
            "SINGLE BLIND", "SINGLE-BLIND", "PLACEBO-CONTROLLED", "PLACEBO CONTROLLED",
            "CROSSOVER STUDY", "CROSS-OVER STUDY", "RANDOMIZED", "RANDOMISED",
        };

        private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "randomized controlled trial", "randomized clinical trial",
            "randomized controlled study", "randomised controlled trial",
            "randomised clinical trial", "cluster randomized controlled trial",
            "observational study", "interventional study", "clinical trial",
            "pilot study", "case-control", "cross-sectional study",
            "prospective study", "retrospective study", "cohort study",
            "longitudinal study", "controlled clinical trial",
            "phase 1", "phase i", "phase 2", "phase ii",
            "phase 3", "phase iii", "phase 4", "phase iv",
            "healthy volunteer study", "healthy subjects", "healthy volunteers",
            "treatment", "safety", "efficacy", "outcomes",
            "patient", "patients", "subjects", "human",
            "participation", "participatory", "measurement",
            "multicenter", "multicentric",
            "diagnosis", "diagnoses", "therapy", "therapies",
            "management", "treatment outcome", "treatment protocol",
            "standard therapy", "best practice", "clinical practice",
            "pathology", "symptom", "symptoms",
            "complication", "complications",
            "prognosis", "mortality", "survival",
            "effectiveness", "evaluation",
        };

        public static string Normalize(string keyword)
        {
            return keyword.Trim()
                .TrimEnd(',', ';', ':', '.', '!', '?')
                .ToUpperInvariant();
        }

        public static (List<string> Cleaned, List<string> Rejected) Filter(
            IEnumerable<string> keywords,
            HashSet<string>? conditions)
        {
            var originalKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cleanedKeywords = originalKeywords
                .Where(k => k.Length >= 4 || (k.Length >= 2 && KnownShortMedicalTerms.Contains(k)))
                .Where(k => k.Length <= 150)
                .Where(k => DesignDescriptorAllowlist.Contains(k) || !Blocklist.Contains(k))
                .Where(k => !k.Contains(';', StringComparison.Ordinal))
                .Where(k => !k.Contains('|', StringComparison.Ordinal))
                .Where(k => k.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 10)
                .Where(k => k.Count(c => c == ',') < 3)
                .Where(k => conditions == null || !conditions.Contains(k))
                .ToList();

            var rejected = originalKeywords.Except(cleanedKeywords, StringComparer.OrdinalIgnoreCase).ToList();
            if (conditions != null)
            {
                rejected = rejected.Where(k => !conditions.Contains(k)).ToList();
            }

            return (cleanedKeywords, rejected);
        }

        /// <summary>
        /// True when a normalized keyword is rejected as generic junk (issue #343).
        /// Design descriptors bypass the blocklist, and generic junk is never
        /// rescued by the MeSH gate — "treatment"/"patient" match MeSH descriptors
        /// but add no signal as keywords.
        /// </summary>
        public static bool IsJunkBlocked(string normalizedKeyword)
        {
            return Blocklist.Contains(normalizedKeyword) && !DesignDescriptorAllowlist.Contains(normalizedKeyword);
        }

        /// <summary>
        /// MeSH gate (issue #343): keywords rejected for structural reasons
        /// (short acronyms, punctuation, length) that still match a MeSH
        /// descriptor at the A/B threshold (&gt;= 0.8) are legitimate medical
        /// terms and are kept. Junk blocklist rejections are excluded by the
        /// caller via <see cref="IsJunkBlocked"/>.
        /// </summary>
        public static (List<string> Accepted, List<string> Rejected) ApplyMeSHGate(
            IReadOnlyCollection<string> rejected,
            IReadOnlyCollection<string> meshMatchedKeywords)
        {
            var matched = meshMatchedKeywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var gateAccepted = rejected.Where(matched.Contains).ToList();
            var stillRejected = rejected.Except(gateAccepted, StringComparer.OrdinalIgnoreCase).ToList();
            return (gateAccepted, stillRejected);
        }
    }
}
