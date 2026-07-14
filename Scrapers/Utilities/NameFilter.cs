using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
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
            "GSK", "CHUGAI",
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
        };

        private static readonly HashSet<string> RolePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CONTACT FOR", "CONTACT", "STUDY DIRECTOR",
            "SCIENTIFIC CONTACT", "PUBLIC QUERIES", "PUBLIC CONTACT",
            "SPONSOR",
        };

        internal static bool IsHumanName(string name, string? role)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (IsNonLatinName(words))
            {
                return trimmed.Length >= 2 && words.Length <= 6;
            }

            if (trimmed.Length < 3)
            {
                return false;
            }

            if (trimmed.Length > 100)
            {
                return false;
            }

            if (words.Length > 6)
            {
                return false;
            }

            if (role != null && KnownPiRoles.Contains(role.Trim()))
            {
                return true;
            }

            var upperName = trimmed.ToUpperInvariant();

            if (RolePrefixes.Any(prefix => upperName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var lastWord = words[^1].TrimEnd(',', '.').ToUpperInvariant();
            if (lastWord is "INC" or "INC." or "LTD" or "LTD." or "LLC" or "CORP"
                or "CORP." or "CORPORATION" or "GMBH" or "AG" or "NV" or "PLC"
                or "SA" or "SARL" or "PTY" or "LIMITED" or "COMPANY" or "CO")
            {
                return false;
            }

            if (trimmed.Contains(" & ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (OrgKeywords.Any(kw => ContainsWord(upperName, kw)))
            {
                return false;
            }

            if (words.Length == 1 && PharmaBlocklist.Contains(words[0].TrimEnd(',', '.')))
            {
                return false;
            }

            if (words.Length == 1 && trimmed.Length > 20)
            {
                return false;
            }

            if (words.Length >= 3 && words.All(w => w.All(c => !char.IsLower(c))))
            {
                return false;
            }

            return true;
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
