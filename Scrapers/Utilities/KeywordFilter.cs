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

        public static (List<string> Cleaned, List<string> Rejected) Filter(
            IEnumerable<string> keywords,
            HashSet<string>? conditions)
        {
            var originalKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Select(k => k.TrimEnd(',', ';', ':', '.', '!', '?'))
                .Select(k => k.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cleanedKeywords = originalKeywords
                .Where(k => k.Length >= 4 || (k.Length >= 2 && KnownShortMedicalTerms.Contains(k)))
                .Where(k => k.Length <= 150)
                .Where(k => !Blocklist.Contains(k))
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
    }
}
