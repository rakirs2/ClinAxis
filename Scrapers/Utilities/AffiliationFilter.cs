using System;
using System.Collections.Generic;

namespace Scrapers.Utilities
{
    internal static class AffiliationFilter
    {
        private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "professor", "director", "chief", "chair", "chairman", "chairperson",
            "surgeon", "specialist", "consultant", "resident", "fellow",
            "nurse", "physician", "doctor", "anesthesiologist", "cardiologist",
            "neurologist", "oncologist", "radiologist", "pathologist", "dermatologist",
            "gastroenterologist", "endocrinologist", "rheumatologist", "nephrologist",
            "pulmonologist", "hematologist", "ophthalmologist", "urologist",
            "psychiatrist", "pediatrician", "researcher", "scientist", "investigator",
            "professor emeritus", "associate professor", "assistant professor",
            "clinical professor", "research professor", "adjunct professor",
            "principle investigator", "principal investigator",
            "co-investigator", "sub-investigator", "study director",
            "medical director", "clinical director", "research director",
            "department head", "section head", "division chief",
            "pharmacist", "therapist", "psychologist", "epidemiologist",
            "biostatistician", "coordinator", "manager", "supervisor",
            "technician", "technologist", "assistant", "associate",
        };

        public static bool IsValidInstitutionName(string affiliation)
        {
            var trimmed = affiliation.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return false;

            if (Blocklist.Contains(trimmed))
                return false;

            if (trimmed.Split(' ').Length == 1 && trimmed.Length > 1)
            {
                if (trimmed.EndsWith("ist", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.EndsWith("ian", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.EndsWith("logist", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}
