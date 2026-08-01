namespace Frontend;

internal sealed class SearchCriteria
{
    public string? Keyword { get; set; }
    public List<string> Statuses { get; set; } = new();
    public List<string> Phases { get; set; } = new();
    public List<string> Conditions { get; set; } = new();
    public List<string> MeshTreePrefixes { get; set; } = new();
    public List<string> LocationMeshTreePrefixes { get; set; } = new();
    public List<string>? Countries { get; set; }
    public List<string>? States { get; set; }
    public string[] Cities { get; set; } = Array.Empty<string>();
    public List<string>? Facilities { get; set; }
    public int? EnrollmentMin { get; set; }
    public int? EnrollmentMax { get; set; }
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
}
