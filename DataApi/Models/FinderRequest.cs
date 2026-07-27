namespace DataApi.Models;

internal class FinderRequest
{
    public string? ConditionTreePrefix { get; set; }
    public string? DrugTreePrefix { get; set; }
    public string? TherapyTreePrefix { get; set; }
    public int TopN { get; set; } = 20;
    public int? MinStudies { get; set; }
    public int? MinHIndex { get; set; }
}
