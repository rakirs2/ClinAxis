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
            "DIRECTOR", "MEDICAL", "STUDY", "CLINICAL",
            "REGISTRY", "MONITOR", "COORDINATOR",
            "MANAGEMENT", "RESPONSIBLE", "CALL CENTER",
            "CENTER", "CORPORATE", "CARE", "TBD",
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
            trimmed = StripTrailingRoleLabel(trimmed);
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return new NameFilterResult(false, "EmptyOrNull");
            }

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

            var lastWord = words[^1].TrimEnd(',', '.').ToUpperInvariant();
            if (lastWord is "INC" or "INC." or "LTD" or "LTD." or "LLC" or "CORP"
                or "CORP." or "CORPORATION" or "GMBH" or "NV" or "PLC"
                or "SA" or "SARL" or "PTY" or "LIMITED" or "COMPANY")
            {
                return new NameFilterResult(false, $"CorporateSuffix:{lastWord}");
            }

            if (trimmed.Contains(" & ", StringComparison.OrdinalIgnoreCase))
            {
                return new NameFilterResult(false, "Ampersand");
            }

            var matchedOrgKw = OrgKeywords.FirstOrDefault(kw => ContainsOrgKeyword(upperName, kw));
            if (matchedOrgKw != null)
            {
                return new NameFilterResult(false, $"OrgKeywords:{matchedOrgKw}");
            }

            if (PharmaBlocklist.Contains(words[0].TrimEnd(',', '.')))
            {
                return new NameFilterResult(false, $"PharmaBlocklist:{words[0].TrimEnd(',', '.')}");
            }

            if (role != null && KnownPiRoles.Contains(role.Trim()))
            {
                return new NameFilterResult(true, null);
            }

            if (words.Length == 1 && trimmed.Length > 20)
            {
                return new NameFilterResult(false, "SingleLongWord");
            }

            return new NameFilterResult(true, null);
        }

        private static bool ContainsOrgKeyword(string upperName, string keyword)
        {
            var words = upperName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var kwWords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (kwWords.Length == 1)
            {
                foreach (var w in words)
                {
                    if (IsWordMatch(w, kwWords[0]))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i + kwWords.Length <= words.Length; i++)
            {
                var matches = true;
                for (int j = 0; j < kwWords.Length; j++)
                {
                    if (!IsWordMatch(words[i + j], kwWords[j]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWordMatch(string word, string keywordWord)
        {
            var trimmedWord = word.TrimEnd(',', '.', ';', ':', '!', '?');
            if (trimmedWord.Equals(keywordWord, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return trimmedWord.Length > keywordWord.Length
                   && trimmedWord.EndsWith('S')
                   && trimmedWord[..^1].Equals(keywordWord, StringComparison.OrdinalIgnoreCase);
        }

        private static string StripTrailingRoleLabel(string trimmed)
        {
            var commaIdx = trimmed.LastIndexOf(',');
            if (commaIdx <= 0)
            {
                return trimmed;
            }

            var label = trimmed[(commaIdx + 1)..].Trim();
            if (label.Length == 0)
            {
                return trimmed;
            }

            var normalized = label.Replace('_', ' ');
            if (RolePrefixes.Contains(normalized) || KnownPiRoles.Contains(normalized))
            {
                return trimmed[..commaIdx].Trim();
            }

            return trimmed;
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
