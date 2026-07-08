using System.Collections.Generic;

namespace Scrapers.Models.ClinicalTrialsGov
{
    public class StudySummary
    {
        public string? NctId { get; set; }
        public string? BriefTitle { get; set; }
        public string? OverallStatus { get; set; }
        public List<string>? Conditions { get; set; }
    }
}