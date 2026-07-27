namespace DataApi.Models;

internal class FinderRequest
{
    public List<string>? ConditionTreePrefixes { get; set; }
    public List<string>? DrugTreePrefixes { get; set; }
    public List<string>? TherapyTreePrefixes { get; set; }
    public int TopN { get; set; } = 20;
    public int? MinStudies { get; set; }
    public int? MinHIndex { get; set; }
}
