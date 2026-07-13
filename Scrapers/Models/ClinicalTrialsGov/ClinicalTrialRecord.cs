using System;
using System.Collections.Generic;

namespace Scrapers.Models.ClinicalTrialsGov
{
    public class ClinicalTrialRecord
    {
        public string? NctId { get; set; }
        public string? BriefTitle { get; set; }
        public string? OfficialTitle { get; set; }
        public string? OverallStatus { get; set; }
        public string? BriefSummary { get; set; }
        public string? StudyType { get; set; }
        public List<string>? Phases { get; set; }
        public List<string>? Conditions { get; set; }
        public List<string>? Keywords { get; set; }
        public string? LeadSponsorName { get; set; }
        public List<string>? CollaboratorNames { get; set; }
        public string? EligibilityCriteria { get; set; }
        public string? Sex { get; set; }
        public string? MinimumAge { get; set; }
        public string? MaximumAge { get; set; }
        public string? HealthyVolunteers { get; set; }
        public string? PrimaryPurpose { get; set; }
        public string? InterventionModel { get; set; }
        public string? Allocation { get; set; }
        public string? Masking { get; set; }
        public string? OrgStudyId { get; set; }
        public int? EnrollmentCount { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? CompletionDate { get; set; }
        public DateOnly? StudyFirstPostDate { get; set; }
        public List<Investigator>? OverallOfficials { get; set; }
        public List<StudyListResponse.Location>? Locations { get; set; }
        public List<Reference>? References { get; set; }
        public List<Outcome>? PrimaryOutcomes { get; set; }
        public List<Outcome>? SecondaryOutcomes { get; set; }
        public List<ArmGroup>? ArmGroups { get; set; }

        public class Reference
        {
            public string? Pmid { get; set; }
            public string? Citation { get; set; }
            public string? Type { get; set; }
        }

        public class Outcome
        {
            public string? Measure { get; set; }
            public string? Description { get; set; }
            public string? TimeFrame { get; set; }
        }

        public class ArmGroup
        {
            public string? Label { get; set; }
            public string? Type { get; set; }
            public string? Description { get; set; }
        }
    }
}
