namespace DataApi.Models;

internal class PageViewRequest
{
    public string? Path { get; set; }

    public string? SessionId { get; set; }

    public DateTime? ViewedAt { get; set; }
}