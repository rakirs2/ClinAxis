using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrapers.Services.Enrichment;

/// <summary>
/// HTTP client for Semantic Scholar API.
/// Searches for investigator metrics (h-index, citation count, etc.) by author name.
/// https://api.semanticscholar.org/
/// </summary>
public sealed class SemanticScholarClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SemanticScholarClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress ??= new Uri("https://api.semanticscholar.org/");
    }

    /// <summary>
    /// Search for an author by name and optional affiliation.
    /// Returns up to 5 results with h-index and citation metrics.
    /// </summary>
    /// <param name="fullName">Full name of the author (e.g., "John Smith")</param>
    /// <param name="affiliation">Optional institution name for disambiguation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Author data if found; null if search fails</returns>
    public async Task<SemanticScholarAuthor?> SearchByNameAsync(string fullName, string? affiliation = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        // Build query: if affiliation is provided, include it for better disambiguation
        var query = fullName.Trim();
        if (!string.IsNullOrWhiteSpace(affiliation))
        {
            query = $"{query} {affiliation.Trim()}";
        }

        var url = $"graph/v1/author/search?query={Uri.EscapeDataString(query)}&fields=hIndex,citationCount,paperCount&limit=5";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var searchResponse = await JsonSerializer.DeserializeAsync<SemanticScholarSearchResponse>(json, _jsonOptions, ct).ConfigureAwait(false);

            if (searchResponse?.Data == null || searchResponse.Data.Count == 0)
                return null;

            // Disambiguation logic: use highest citation count as a tiebreaker
            // This ensures we pick the most prominent researcher with the matching name
            var bestMatch = searchResponse.Data
                .OrderByDescending(a => a.CitationCount ?? 0)
                .ThenByDescending(a => a.HIndex ?? 0)
                .FirstOrDefault();

            return bestMatch;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

/// <summary>
/// Search response from Semantic Scholar API.
/// </summary>
public sealed class SemanticScholarSearchResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("data")]
    public List<SemanticScholarAuthor>? Data { get; set; }
}

/// <summary>
/// Single author result from Semantic Scholar API.
/// </summary>
public sealed class SemanticScholarAuthor
{
    [JsonPropertyName("authorId")]
    public string? AuthorId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("paperCount")]
    public int? PaperCount { get; set; }

    [JsonPropertyName("citationCount")]
    public int? CitationCount { get; set; }

    [JsonPropertyName("hIndex")]
    public int? HIndex { get; set; }

    /// <summary>
    /// I10-Index: number of papers with 10 or more citations.
    /// May not be returned by all API versions.
    /// </summary>
    [JsonPropertyName("i10Index")]
    public int? I10Index { get; set; }
}
