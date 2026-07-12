using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrapers.Services.Cms;

public sealed class OrcidApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public OrcidApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress ??= new Uri("https://pub.orcid.org/v3.0/");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    }

    public async Task<string?> LookupOrcidAsync(string firstName, string lastName, CancellationToken ct = default)
    {
        var results = await SearchByNameAsync(firstName, lastName, ct).ConfigureAwait(false);
        return results.Count > 0 ? results[0].Path : null;
    }

    public async Task<IReadOnlyList<OrcidResult>> SearchByNameAsync(string firstName, string lastName, CancellationToken ct = default)
    {
        var query = $"family-name:{Uri.EscapeDataString(lastName)}+AND+given-name:{Uri.EscapeDataString(firstName)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/search?q={query}", UriKind.Relative));
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<OrcidSearchResponse>(json, _jsonOptions, ct).ConfigureAwait(false);

        return envelope?.Result?.Select(r => r.OrcidIdentifier).ToList() ?? [];
    }
}

public sealed class OrcidSearchResponse
{
    [JsonPropertyName("num-found")]
    public int NumFound { get; set; }
    public IReadOnlyList<OrcidSearchResultItem>? Result { get; set; }
}

public sealed class OrcidSearchResultItem
{
    [JsonPropertyName("orcid-identifier")]
    public OrcidResult OrcidIdentifier { get; set; } = new();
}

public sealed class OrcidResult
{
    public Uri? Uri { get; set; }
    public string? Path { get; set; }
    public string? Host { get; set; }
}
