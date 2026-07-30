using System.Collections.ObjectModel;

namespace Frontend;

internal sealed class BlogPostInfo
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public Collection<string> Tags { get; init; } = [];
    public string Category { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
}
