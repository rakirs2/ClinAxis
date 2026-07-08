namespace Scrapers.Models.ClinicalTrialsGov;

public sealed record ClinicalTrialRecord(StudySummary Summary, IReadOnlyList<Investigator> Investigators);
