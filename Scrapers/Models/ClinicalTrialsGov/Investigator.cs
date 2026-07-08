namespace Scrapers.Models.ClinicalTrialsGov;

public sealed record Investigator(string? Name, string? Affiliation, string? Role)
{
    public bool HasName => !string.IsNullOrWhiteSpace(Name);
}
