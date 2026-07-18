using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
    public record NameFilterResult(bool IsHuman, string? RejectionReason);

    internal static class NameFilter
    {
        private static readonly HashSet<string> KnownPiRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "PRINCIPAL_INVESTIGATOR", "SUB_INVESTIGATOR", "STUDY_DIRECTOR",
            "STUDY_CHAIR", "STUDY_CO_CHAIR", "STUDY_COORDINATOR",
            "INVESTIGATOR", "CO_INVESTIGATOR",
        };

        private static readonly HashSet<string> PharmaBlocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "PFIZER", "ROCHE", "NOVARTIS", "ASTRAZENECA", "MERCK",
            "SANOFI", "BAYER", "TAKEDA", "AMGEN", "GILEAD",
            "BIOGEN", "ABBVIE", "REGENERON", "MODERNA", "BIONTECH",
            "CELGENE", "MYLAN", "TEVA", "SANDOZ",
            "MEDTRONIC", "STRYKER", "BAUSCH",
            "VIATRIS", "BOEHRINGER",
            "GSK", "CHUGAI", "UCB",
            "BRISTOL-MYERS", "BRISTOL",
            "MEDIMMUNE",
        };

        private static readonly HashSet<string> KnownNonHumanNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "USE CENTRAL CONTACT",
            "BRISTOL-MYERS SQUIBB",
            "BRISTOL MYERS SQUIBB",
        };

        private static readonly HashSet<string> OrgKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "UNIVERSITY", "COLLEGE", "INSTITUTE", "HOSPITAL", "CLINIC",
            "LABORATORY", "LABORATORIES", "LAB", "FOUNDATION",
            "DEPARTMENT", "COMMITTEE", "ASSOCIATION", "CORPORATION",
            "COMPANY", "PHARMA", "PHARMACEUTICAL", "BIOTECH",
            "BIOSCIENCE", "BIOSCIENCES", "THERAPEUTICS",
            "MEDICAL CENTER", "CANCER CENTER", "HEALTH SYSTEM",
            "HEALTHCARE", "NATIONAL INSTITUTE", "SCHOOL OF",
            "COLLEGE OF", "OFFICE OF", "CENTER FOR", "CENTRE FOR",
            "INSTITUTE OF", "DIVISION OF", "DEPARTMENT OF",
            "BOARD OF", "MINISTRY OF", "FUND FOR",
            "RESEARCH INSTITUTE", "RESEARCH CENTER", "RESEARCH CENTRE",
            "CLINICAL RESEARCH", "CLINICAL TRIAL", "CLINICAL TRIALS",
            "LIMITED LIABILITY", "SOCIETE", "GESELLSCHAFT", "GMBH",
            "AKTIENGESELLSCHAFT", "AG", "NV", "PTY", "PTY LTD",
            "AND ASSOCIATES", "AND COMPANY", "& CO", "& ASSOCIATES",
            "DIRECTOR", "MEDICAL", "CLINICAL",
            "REGISTRY", "MONITOR",
            "CALL CENTER",
            "CORPORATE", "TBD",
            "SPONSOR",
        };

        private static readonly HashSet<string> RolePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CONTACT FOR", "CONTACT", "STUDY DIRECTOR",
            "SCIENTIFIC CONTACT", "PUBLIC QUERIES", "PUBLIC CONTACT",
            "SPONSOR",
        };

        internal static NameFilterResult IsHumanName(string name, string? role)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new NameFilterResult(false, "EmptyOrNull");
            }

            var trimmed = name.Trim();
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (IsNonLatinName(words))
            {
                var ok = trimmed.Length >= 2 && words.Length <= 6;
                return ok
                    ? new NameFilterResult(true, null)
                    : new NameFilterResult(false, "NonLatinName");
            }

            if (trimmed.Length < 3)
            {
                return new NameFilterResult(false, "TooShort");
            }

            if (trimmed.Length > 100)
            {
                return new NameFilterResult(false, "TooLong");
            }

            if (words.Length > 10)
            {
                return new NameFilterResult(false, "TooManyWords");
            }

            var upperName = trimmed.ToUpperInvariant();

            if (KnownNonHumanNames.Contains(upperName))
            {
                return new NameFilterResult(false, $"KnownNonHumanName:{upperName}");
            }

            var matchedPrefix = RolePrefixes.FirstOrDefault(prefix => upperName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (matchedPrefix != null)
            {
                return new NameFilterResult(false, $"RolePrefix:{matchedPrefix}");
            }

            if (words[0].StartsWith('+') && words[0].Any(c => c is >= '0' and <= '9'))
            {
                return new NameFilterResult(false, "PhonePrefix");
            }

            if (IsNumeric(words[0]))
            {
                return new NameFilterResult(false, "DigitPrefix");
            }

            var lastWord = words[^1].TrimEnd(',', '.').ToUpperInvariant();
            if (lastWord is "INC" or "INC." or "LTD" or "LTD." or "LLC" or "CORP"
                or "CORP." or "CORPORATION" or "GMBH" or "AG" or "NV" or "PLC"
                or "SA" or "SARL" or "PTY" or "LIMITED" or "COMPANY" or "CO")
            {
                return new NameFilterResult(false, $"CorporateSuffix:{lastWord}");
            }

            if (trimmed.Contains(" & ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(" & ", StringSplitOptions.TrimEntries);
                var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MD", "M.D", "PHD", "PH.D", "MBA", "DO", "DDS", "FRCPC", "FAAN", "L.AC", "LAC" };
                if (parts.Length > 1 && parts.All(p => suffixes.Contains(p.TrimEnd(',').TrimEnd('.').Trim())))
                {
                    goto AfterAmpersand;
                }
                return new NameFilterResult(false, "Ampersand");
            }
        AfterAmpersand:

            if (PharmaBlocklist.Contains(words[0].TrimEnd(',', '.')))
            {
                return new NameFilterResult(false, $"PharmaBlocklist:{words[0].TrimEnd(',', '.')}");
            }

            if (role != null && KnownPiRoles.Contains(role.Trim()))
            {
                return new NameFilterResult(true, null);
            }

            var matchedOrgKw = OrgKeywords.FirstOrDefault(kw => ContainsWord(upperName, kw));
            if (matchedOrgKw != null)
            {
                return new NameFilterResult(false, $"OrgKeywords:{matchedOrgKw}");
            }

            if (words.Length == 1 && trimmed.Length > 20)
            {
                return new NameFilterResult(false, "SingleLongWord");
            }

            return new NameFilterResult(true, null);
        }

        private static bool ContainsWord(string text, string word)
        {
            if (text.Length < word.Length)
            {
                return false;
            }

            var upperWord = word.ToUpperInvariant();
            return text.Contains(upperWord, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumeric(string s)
        {
            foreach (var c in s)
            {
                if (c is < '0' or > '9') return false;
            }
            return s.Length > 0;
        }

        private static bool IsNonLatinName(string[] words)
        {
            foreach (var word in words)
            {
                foreach (var c in word)
                {
                    if (char.IsLetter(c) && !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
