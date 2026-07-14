using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
    internal static class NameParser
    {
        private static readonly string[] Prefixes =
        [
            "DR", "PROF", "MR", "MRS", "MS", "MX", "SIR", "DAME",
            "CAPT", "COL", "MAJ", "LT", "CPT", "CPL", "SGT",
            "REV", "FR", "PASTOR", "RABBI", "IMAM",
            "HON", "SEN", "GOV", "REP", "AMB",
        ];

        private static readonly string[] Suffixes =
        [
            "MD", "DO", "DDS", "DMD", "PHD", "EDD", "JD", "LLB", "LLM",
            "PHARMD", "RPH",
            "MPH", "MSN", "RN", "NP", "PA", "CNS", "APRNP",
            "MBA", "MS", "MA", "BS", "BA", "BSC",
            "FACS", "FACE", "FACC", "ASCO", "FASH",
            "DC", "OD", "DPM", "DVM", "VMD",
            "JR", "SR", "II", "III", "IV",
        ];

        internal static (string? Prefix, string FullName, string? Suffix) Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (null, string.Empty, null);
            }

            var trimmed = raw.Trim();
            string? prefix = null;
            var suffixes = new List<string>();

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length > 0 && MatchesPrefix(parts[0]))
            {
                prefix = parts[0];
                parts = parts[1..];
                trimmed = string.Join(" ", parts);
            }

            while (parts.Length > 0 && MatchesSuffix(Stripped(parts[^1])))
            {
                suffixes.Add(parts[^1].TrimEnd(','));
                parts = parts[..^1];
                trimmed = string.Join(" ", parts);
            }

            if (suffixes.Count == 0)
            {
                var commaIdx = trimmed.LastIndexOf(", ", StringComparison.Ordinal);
                if (commaIdx > 0)
                {
                    var afterCommaParts = trimmed[(commaIdx + 2)..].Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var parsedSuffixes = new List<string>();
                    var allMatch = afterCommaParts.All(p => MatchesSuffix(Stripped(p)));
                    if (allMatch)
                    {
                        foreach (var sp in afterCommaParts)
                        {
                            parsedSuffixes.Add(sp.TrimEnd(',', '.'));
                        }

                        suffixes = parsedSuffixes;
                        trimmed = trimmed[..commaIdx].Trim();
                    }
                }
            }

            var fullName = trimmed.TrimEnd(',').Trim();
            suffixes.Reverse();
            var suffix = suffixes.Count > 0 ? string.Join(", ", suffixes) : null;
            return (prefix, fullName, suffix);
        }

        private static string Stripped(string word) =>
            word.TrimEnd(',', '.').ToUpperInvariant();

        private static bool MatchesPrefix(string word) =>
            Array.IndexOf(Prefixes, Stripped(word)) >= 0;

        private static bool MatchesSuffix(string word) =>
            Array.IndexOf(Suffixes, word) >= 0;
    }
}
