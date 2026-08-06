namespace DataApi.Models;

internal class RecommendRequest
{
    public List<string>? TherapyTreePrefixes { get; set; }
    public List<string>? ConditionTreePrefixes { get; set; }
    public string? Population { get; set; }
    public string? Region { get; set; }
    public int? EnrollmentTarget { get; set; }
    public int? Phase { get; set; }
    public int TopN { get; set; } = 20;

    public List<string> GetAllPrefixes()
    {
        var prefixes = new List<string>();
        if (TherapyTreePrefixes?.Count > 0) prefixes.AddRange(TherapyTreePrefixes);
        if (ConditionTreePrefixes?.Count > 0) prefixes.AddRange(ConditionTreePrefixes);
        return prefixes;
    }
}

internal class RecommendFactors
{
    public double? Experience { get; set; }
    public double? Completion { get; set; }
    public double? ConditionFit { get; set; }
    public double? Velocity { get; set; }
    public double? Publication { get; set; }
    public double? Geographic { get; set; }
}

internal class RecommendDetails
{
    public int StudyCount { get; set; }
    public double? CompletionRate { get; set; }
    public double? EnrollmentVelocity { get; set; }
    public int? HIndex { get; set; }
    public int? PaperCount { get; set; }
    public List<string> ConditionCategories { get; set; } = [];
    public List<string> Regions { get; set; } = [];
}

internal class RecommendRank
{
    public Guid Uuid { get; set; }
    public string? Name { get; set; }
    public string? PrimaryAffiliation { get; set; }
    public double Score { get; set; }
    public RecommendFactors Factors { get; set; } = new();
    public RecommendDetails Details { get; set; } = new();
}

internal class RecommendResponse
{
    public int TotalCandidates { get; set; }
    public RecommendRequestSummary Request { get; set; } = new();
    public List<RecommendRank> Investigators { get; set; } = [];
}

internal class RecommendRequestSummary
{
    public List<string> TherapyTreePrefixes { get; set; } = [];
    public List<string> ConditionTreePrefixes { get; set; } = [];
    public string? Population { get; set; }
    public string? Region { get; set; }
    public int? EnrollmentTarget { get; set; }
    public int? Phase { get; set; }
}
