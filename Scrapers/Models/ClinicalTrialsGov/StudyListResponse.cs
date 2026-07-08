using System.Text.Json.Serialization;

namespace Scrapers.Models.ClinicalTrialsGov;

internal sealed class StudyListResponse
{
    [JsonPropertyName("studies")]
    public IReadOnlyList<StudyPayload>? Studies { get; init; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; init; }

    internal sealed class StudyPayload
    {
        [JsonPropertyName("protocolSection")]
        public ProtocolSection? ProtocolSection { get; init; }

        public StudySummary? ToSummary()
        {
            var identification = ProtocolSection?.IdentificationModule;
            if (identification is null)
            {
                return null;
            }

            return new StudySummary(
                identification.NctId,
                identification.BriefTitle,
                ProtocolSection?.StatusModule?.OverallStatus);
        }

        public ClinicalTrialRecord? ToRecord()
        {
            var summary = ToSummary();
            if (summary is null)
            {
                return null;
            }

            var investigators = ProtocolSection?.ContactsLocationsModule?.OverallOfficials?
                .Where(o => !string.IsNullOrWhiteSpace(o?.Name))
                .Select(o => new Investigator(o!.Name, o.Affiliation, o.Role))
                .ToArray() ?? Array.Empty<Investigator>();

            return new ClinicalTrialRecord(summary, investigators);
        }
    }

    internal sealed class ProtocolSection
    {
        [JsonPropertyName("identificationModule")]
        public IdentificationModule? IdentificationModule { get; init; }

        [JsonPropertyName("statusModule")]
        public StatusModule? StatusModule { get; init; }

        [JsonPropertyName("contactsLocationsModule")]
        public ContactsLocationsModule? ContactsLocationsModule { get; init; }
    }

    internal sealed class IdentificationModule
    {
        [JsonPropertyName("nctId")]
        public string? NctId { get; init; }

        [JsonPropertyName("briefTitle")]
        public string? BriefTitle { get; init; }
    }

    internal sealed class StatusModule
    {
        [JsonPropertyName("overallStatus")]
        public string? OverallStatus { get; init; }
    }

    internal sealed class ContactsLocationsModule
    {
        [JsonPropertyName("overallOfficials")]
        public IReadOnlyList<Official>? OverallOfficials { get; init; }
    }

    internal sealed class Official
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("affiliation")]
        public string? Affiliation { get; init; }

        [JsonPropertyName("role")]
        public string? Role { get; init; }
    }
}
