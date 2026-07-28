using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;

namespace Frontend;

internal sealed partial class BlogService
{
    private readonly string _blogPath;
    private List<BlogPostInfo>? _cache;

    public BlogService(IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);

        _blogPath = Path.Combine(env.ContentRootPath, "blog");
        if (!Directory.Exists(_blogPath))
        {
            var devPath = Path.Combine(env.ContentRootPath, "..", "blog");
            if (Directory.Exists(devPath))
                _blogPath = Path.GetFullPath(devPath);
        }
    }

    public async Task<List<BlogPostInfo>> GetAllPostsAsync()
    {
        _cache ??= await LoadAllPostsAsync().ConfigureAwait(false);
        return _cache;
    }

    public async Task<BlogPostInfo?> GetPostBySlugAsync(string slug)
    {
        var posts = await GetAllPostsAsync().ConfigureAwait(false);
        return posts.FirstOrDefault(p =>
            p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<BlogPostInfo>> LoadAllPostsAsync()
    {
        if (!Directory.Exists(_blogPath))
            return [];

        var files = Directory.GetFiles(_blogPath, "*.md", SearchOption.AllDirectories);
        var posts = new List<BlogPostInfo>();

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            var post = ParsePost(file, content);
            if (post is not null)
                posts.Add(post);
        }

        return [.. posts.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.Date)];
    }

    private static BlogPostInfo? ParsePost(string filePath, string content)
    {
        var match = FrontMatterRegex().Match(content);
        if (!match.Success)
            return null;

        var frontRaw = match.Groups[1].Value;
        var bodyMd = content[match.Length..].Trim();

        var title = ExtractField(frontRaw, "title") ?? Path.GetFileNameWithoutExtension(filePath);
        var dateStr = ExtractField(frontRaw, "date");
        var tagsRaw = ExtractField(frontRaw, "tags");
        var category = ExtractField(frontRaw, "category") ?? "uncategorized";

        var pinnedRaw = ExtractField(frontRaw, "pinned");
        var isPinned = pinnedRaw is not null && bool.TryParse(pinnedRaw, out var pinned) && pinned;

        DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);

        var tags = new Collection<string>();
        if (tagsRaw is not null)
        {
            foreach (var t in tagsRaw.TrimStart('[').TrimEnd(']').Split(','))
            {
                var trimmed = t.Trim('\'', '"', ' ');
                if (trimmed.Length > 0)
                    tags.Add(trimmed);
            }
        }

        var slug = Path.GetFileNameWithoutExtension(filePath)
            .Replace(" ", "-", StringComparison.Ordinal);

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var bodyHtml = Markdown.ToHtml(bodyMd, pipeline);

        return new BlogPostInfo
        {
            Title = title,
            Slug = slug,
            Date = date,
            Tags = tags,
            Category = category,
            BodyHtml = bodyHtml,
            IsPinned = isPinned
        };
    }

    private static string? ExtractField(string frontMatter, string key)
    {
        var match = Regex.Match(frontMatter, $@"^{key}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---", RegexOptions.Singleline)]
    private static partial Regex FrontMatterRegex();
}
