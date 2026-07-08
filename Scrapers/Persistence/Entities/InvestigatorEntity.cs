namespace Scrapers.Persistence.Entities;

public class InvestigatorEntity
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Affiliation { get; set; }
    public string? Role { get; set; }

    public StudyEntity? Study { get; set; }
}
