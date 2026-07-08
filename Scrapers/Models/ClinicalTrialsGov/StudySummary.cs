namespace Scrapers.Models.ClinicalTrialsGov;

/// <summary>
/// Represents a lightweight slice of a ClinicalTrials.gov study record.
/// </summary>
public sealed record StudySummary(string? NctId, string? BriefTitle, string? OverallStatus)
{
    public override string ToString()
    {
        var id = string.IsNullOrWhiteSpace(NctId) ? "<unknown>" : NctId;
        var title = string.IsNullOrWhiteSpace(BriefTitle) ? "Untitled Study" : BriefTitle;
        var status = string.IsNullOrWhiteSpace(OverallStatus) ? "Status Unknown" : OverallStatus;

        return $"{id} | {status} | {title}";
    }
}
