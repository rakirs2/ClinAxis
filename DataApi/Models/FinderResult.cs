using System.Collections.ObjectModel;

namespace DataApi.Models;

internal class FinderResponse
{
    public Collection<InvestigatorRank> Investigators { get; } = [];
    public int TotalCandidates { get; set; }
}

internal class InvestigatorRank
{
    public Guid Uuid { get; set; }
    public string? Name { get; set; }
    public string? PrimaryAffiliation { get; set; }
    public double Score { get; set; }
    public double? ModelScore { get; set; }
    public ScoreFactors Factors { get; set; } = new();
    public InvestigatorDetails Details { get; set; } = new();
}

internal class ScoreFactors
{
    public double Relevance { get; set; }
    public double Experience { get; set; }
    public double Publication { get; set; }
    public double Network { get; set; }
}

internal class InvestigatorDetails
{
    public int StudyCount { get; set; }
    public int CompletedStudies { get; set; }
    public int? EnrollmentTotal { get; set; }
    public int? HIndex { get; set; }
    public int? PaperCount { get; set; }
}
