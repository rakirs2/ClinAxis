using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Scrapers.Models.ClinicalTrialsGov
{
    public class StudyListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }

        [JsonPropertyName("studies")]
        public List<StudyPayload>? Studies { get; set; }

        public class StudyPayload
        {
            [JsonPropertyName("protocolSection")]
            public ProtocolSection? ProtocolSection { get; set; }
        }

        public class ProtocolSection
        {
            [JsonPropertyName("identificationModule")]
            public IdentificationModule? IdentificationModule { get; set; }

            [JsonPropertyName("statusModule")]
            public StatusModule? StatusModule { get; set; }

            [JsonPropertyName("sponsorCollaboratorsModule")]
            public SponsorCollaboratorsModule? SponsorCollaboratorsModule { get; set; }

            [JsonPropertyName("descriptionModule")]
            public DescriptionModule? DescriptionModule { get; set; }

            [JsonPropertyName("conditionsModule")]
            public ConditionsModule? ConditionsModule { get; set; }

            [JsonPropertyName("designModule")]
            public DesignModule? DesignModule { get; set; }

            [JsonPropertyName("armsInterventionsModule")]
            public ArmsInterventionsModule? ArmsInterventionsModule { get; set; }

            [JsonPropertyName("outcomesModule")]
            public OutcomesModule? OutcomesModule { get; set; }

            [JsonPropertyName("eligibilityModule")]
            public EligibilityModule? EligibilityModule { get; set; }

            [JsonPropertyName("contactsLocationsModule")]
            public ContactsLocationsModule? ContactsLocationsModule { get; set; }

            [JsonPropertyName("referencesModule")]
            public ReferencesModule? ReferencesModule { get; set; }
        }

        public class IdentificationModule
        {
            [JsonPropertyName("nctId")]
            public string? NctId { get; set; }

            [JsonPropertyName("briefTitle")]
            public string? BriefTitle { get; set; }

            [JsonPropertyName("officialTitle")]
            public string? OfficialTitle { get; set; }

            [JsonPropertyName("orgStudyIdInfo")]
            public OrgStudyIdInfo? OrgStudyIdInfo { get; set; }
        }

        public class OrgStudyIdInfo
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
        }

        public class StatusModule
        {
            [JsonPropertyName("overallStatus")]
            public string? OverallStatus { get; set; }

            [JsonPropertyName("startDateStruct")]
            public DateStruct? StartDateStruct { get; set; }

            [JsonPropertyName("completionDateStruct")]
            public DateStruct? CompletionDateStruct { get; set; }

            [JsonPropertyName("studyFirstPostDateStruct")]
            public DateStruct? StudyFirstPostDateStruct { get; set; }
        }

        public class DateStruct
        {
            [JsonPropertyName("date")]
            public string? Date { get; set; }
        }

        public class SponsorCollaboratorsModule
        {
            [JsonPropertyName("leadSponsor")]
            public Sponsor? LeadSponsor { get; set; }

            [JsonPropertyName("collaborators")]
            public List<Sponsor>? Collaborators { get; set; }
        }

        public class Sponsor
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        public class DescriptionModule
        {
            [JsonPropertyName("briefSummary")]
            public string? BriefSummary { get; set; }
        }

        public class ConditionsModule
        {
            [JsonPropertyName("conditions")]
            public List<string>? Conditions { get; set; }

            [JsonPropertyName("keywords")]
            public List<string>? Keywords { get; set; }
        }

        public class DesignModule
        {
            [JsonPropertyName("studyType")]
            public string? StudyType { get; set; }

            [JsonPropertyName("phases")]
            public List<string>? Phases { get; set; }

            [JsonPropertyName("designInfo")]
            public DesignInfo? DesignInfo { get; set; }

            [JsonPropertyName("enrollmentInfo")]
            public EnrollmentInfo? EnrollmentInfo { get; set; }
        }

        public class DesignInfo
        {
            [JsonPropertyName("allocation")]
            public string? Allocation { get; set; }

            [JsonPropertyName("interventionModel")]
            public string? InterventionModel { get; set; }

            [JsonPropertyName("primaryPurpose")]
            public string? PrimaryPurpose { get; set; }

            [JsonPropertyName("masking")]
            public string? Masking { get; set; }
        }

        public class EnrollmentInfo
        {
            [JsonPropertyName("count")]
            public int? Count { get; set; }
        }

        public class ArmsInterventionsModule
        {
            [JsonPropertyName("armGroups")]
            public List<ArmGroup>? ArmGroups { get; set; }
        }

        public class ArmGroup
        {
            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }
        }

        public class OutcomesModule
        {
            [JsonPropertyName("primaryOutcomes")]
            public List<Outcome>? PrimaryOutcomes { get; set; }

            [JsonPropertyName("secondaryOutcomes")]
            public List<Outcome>? SecondaryOutcomes { get; set; }
        }

        public class Outcome
        {
            [JsonPropertyName("measure")]
            public string? Measure { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("timeFrame")]
            public string? TimeFrame { get; set; }
        }

        public class EligibilityModule
        {
            [JsonPropertyName("eligibilityCriteria")]
            public string? EligibilityCriteria { get; set; }

            [JsonPropertyName("sex")]
            public string? Sex { get; set; }

            [JsonPropertyName("minimumAge")]
            public string? MinimumAge { get; set; }

            [JsonPropertyName("maximumAge")]
            public string? MaximumAge { get; set; }

            [JsonPropertyName("healthyVolunteers")]
            public System.Text.Json.JsonElement HealthyVolunteers { get; set; }
        }

        public class ContactsLocationsModule
        {
            [JsonPropertyName("overallOfficials")]
            public List<OverallOfficial>? OverallOfficials { get; set; }

            [JsonPropertyName("locations")]
            public List<Location>? Locations { get; set; }
        }

        public class OverallOfficial
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("affiliation")]
            public string? Affiliation { get; set; }

            [JsonPropertyName("role")]
            public string? Role { get; set; }
        }

        public class Location
        {
            [JsonPropertyName("facility")]
            public string? Facility { get; set; }

            [JsonPropertyName("city")]
            public string? City { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }

            [JsonPropertyName("country")]
            public string? Country { get; set; }
        }

        public class ReferencesModule
        {
            [JsonPropertyName("references")]
            public List<Reference>? References { get; set; }
        }

        public class Reference
        {
            [JsonPropertyName("pmid")]
            public string? Pmid { get; set; }

            [JsonPropertyName("pmcid")]
            public string? Pmcid { get; set; }

            [JsonPropertyName("doi")]
            public string? Doi { get; set; }

            [JsonPropertyName("citation")]
            public string? Citation { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }
    }

    internal static class StudyPayloadExtensions
    {
        internal static string? ConvertHealthyVolunteers(System.Text.Json.JsonElement element)
        {
            return element.ValueKind == System.Text.Json.JsonValueKind.String
                ? element.GetString()
                : element.ValueKind == System.Text.Json.JsonValueKind.True
                    ? "true"
                    : element.ValueKind == System.Text.Json.JsonValueKind.False ? "false" : null;
        }

        private static DateOnly? ParseDateStruct(StudyListResponse.DateStruct? ds)
        {
            if (ds?.Date == null)
            {
                return null;
            }

            var parts = ds.Date.Split('-');
            if (parts.Length >= 1 && int.TryParse(parts[0], out var year))
            {
                var month = parts.Length >= 2 && int.TryParse(parts[1], out var m) ? m : 1;
                var day = parts.Length >= 3 && int.TryParse(parts[2], out var d) ? d : 1;
                return new DateOnly(year, month, day);
            }
            return null;
        }

        internal static StudySummary ToSummary(this StudyListResponse.StudyPayload payload)
        {
            StudyListResponse.ProtocolSection? ps = payload.ProtocolSection;
            return new StudySummary
            {
                NctId = ps?.IdentificationModule?.NctId,
                BriefTitle = ps?.IdentificationModule?.BriefTitle,
                OverallStatus = ps?.StatusModule?.OverallStatus,
                Conditions = ps?.ConditionsModule?.Conditions
            };
        }

        internal static ClinicalTrialRecord ToRecord(this StudyListResponse.StudyPayload payload)
        {
            StudyListResponse.ProtocolSection? ps = payload.ProtocolSection;
            return ps == null
                ? new ClinicalTrialRecord()
                : new ClinicalTrialRecord
                {
                    NctId = ps.IdentificationModule?.NctId,
                    BriefTitle = ps.IdentificationModule?.BriefTitle,
                    OfficialTitle = ps.IdentificationModule?.OfficialTitle,
                    OverallStatus = ps.StatusModule?.OverallStatus,
                    BriefSummary = ps.DescriptionModule?.BriefSummary,
                    StudyType = ps.DesignModule?.StudyType,
                    Phases = ps.DesignModule?.Phases,
                    Conditions = ps.ConditionsModule?.Conditions,
                    Keywords = ps.ConditionsModule?.Keywords,
                    LeadSponsorName = ps.SponsorCollaboratorsModule?.LeadSponsor?.Name,
                    CollaboratorNames = ps.SponsorCollaboratorsModule?.Collaborators?.Select(c => c.Name!).ToList(),
                    EligibilityCriteria = ps.EligibilityModule?.EligibilityCriteria,
                    Sex = ps.EligibilityModule?.Sex,
                    MinimumAge = ps.EligibilityModule?.MinimumAge,
                    MaximumAge = ps.EligibilityModule?.MaximumAge,
                    HealthyVolunteers = StudyPayloadExtensions.ConvertHealthyVolunteers(ps.EligibilityModule?.HealthyVolunteers ?? default),
                    PrimaryPurpose = ps.DesignModule?.DesignInfo?.PrimaryPurpose,
                    InterventionModel = ps.DesignModule?.DesignInfo?.InterventionModel,
                    Allocation = ps.DesignModule?.DesignInfo?.Allocation,
                    EnrollmentCount = ps.DesignModule?.EnrollmentInfo?.Count,
                    StartDate = ParseDateStruct(ps.StatusModule?.StartDateStruct),
                    CompletionDate = ParseDateStruct(ps.StatusModule?.CompletionDateStruct),
                    StudyFirstPostDate = ParseDateStruct(ps.StatusModule?.StudyFirstPostDateStruct),
                    OverallOfficials = ps.ContactsLocationsModule?.OverallOfficials?
                    .Select(o => new Investigator { Name = o.Name, Role = o.Role, Affiliation = o.Affiliation }).ToList(),
                    Locations = ps.ContactsLocationsModule?.Locations,
                    References = ps.ReferencesModule?.References?.Select(r => new ClinicalTrialRecord.Reference
                    {
                        Pmid = r.Pmid,
                        Pmcid = r.Pmcid,
                        Doi = r.Doi,
                        Citation = r.Citation,
                        Type = r.Type
                    }).ToList()
                };
        }
    }
}
