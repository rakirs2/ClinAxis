namespace DataApi.Models;

internal class FinderRequest
{
    public List<string>? TreePrefixes { get; set; }
    public int TopN { get; set; } = 20;
    public int? MinStudies { get; set; }
    public int? MinHIndex { get; set; }

    // Kept for backward compat with old frontend payloads
    public List<string>? ConditionTreePrefixes { get; set; }
    public List<string>? DrugTreePrefixes { get; set; }
    public List<string>? TherapyTreePrefixes { get; set; }

    public List<string> GetAllPrefixes()
    {
        var all = new List<string>();
        if (TreePrefixes is { Count: > 0 }) all.AddRange(TreePrefixes);
        if (ConditionTreePrefixes is { Count: > 0 }) all.AddRange(ConditionTreePrefixes);
        if (DrugTreePrefixes is { Count: > 0 }) all.AddRange(DrugTreePrefixes);
        if (TherapyTreePrefixes is { Count: > 0 }) all.AddRange(TherapyTreePrefixes);
        return all;
    }
}
