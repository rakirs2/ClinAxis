using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities;

public class ScrapeEventEntity
{
    [Key]
    public int Id { get; set; }
    public int? PipelineRunId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public long? DurationMs { get; set; }
    public int? RecordsAffected { get; set; }
    public string? Message { get; set; }
    public int? HttpStatusCode { get; set; }

    [ForeignKey(nameof(PipelineRunId))]
    public PipelineRunEntity? PipelineRun { get; set; }
}
