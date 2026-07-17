using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrapers.Services.Enrichment;

public sealed class OrcidApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public OrcidApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress ??= new Uri("https://pub.orcid.org/v3.0/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<IReadOnlyList<OrcidSearchResult>> SearchByNameAsync(string givenName, string familyName, int maxResults = 10, CancellationToken ct = default)
    {
        var query = $"given-and-family-names:\"{Uri.EscapeDataString(givenName)} {Uri.EscapeDataString(familyName)}\"";
        var url = $"search?q={query}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<OrcidSearchResponse>(json, _jsonOptions, ct).ConfigureAwait(false);

        return envelope?.Result?
            .Take(maxResults)
            .Select(r => new OrcidSearchResult
            {
                Orcid = r?.OrcidIdentifier?.Path ?? ""
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Orcid))
            .ToList() ?? [];
    }

    public async Task<OrcidRecord?> GetRecordAsync(string orcid, CancellationToken ct = default)
    {
        var url = $"{orcid}/record";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<OrcidRecord>(json, _jsonOptions, ct).ConfigureAwait(false);
    }
}

public sealed class OrcidSearchResult
{
    public string Orcid { get; set; } = "";
}

public sealed class OrcidRecord
{
    public OrcidPerson? Person { get; set; }
}

public sealed class OrcidPerson
{
    public OrcidName? Name { get; set; }
    [JsonPropertyName("external-identifiers")]
    public OrcidExternalIdentifiers? ExternalIdentifiers { get; set; }
}

public sealed class OrcidName
{
    [JsonPropertyName("given-names")]
    public OrcidValue? GivenNames { get; set; }
    [JsonPropertyName("family-name")]
    public OrcidValue? FamilyNames { get; set; }
}

public sealed class OrcidValue
{
    public string? Value { get; set; }
}

public sealed class OrcidExternalIdentifiers
{
    [JsonPropertyName("external-identifier")]
    public List<OrcidExternalIdentifier>? ExternalIdentifier { get; set; }
}

public sealed class OrcidExternalIdentifier
{
    [JsonPropertyName("external-id-type")]
    public string? ExternalIdType { get; set; }
    [JsonPropertyName("external-id-value")]
    public string? ExternalIdValue { get; set; }
    [JsonPropertyName("external-id-url")]
    public OrcidUrl? ExternalIdUrl { get; set; }
}

public sealed class OrcidUrl
{
    public string? Value { get; set; }
}

// ORCID search response models
public sealed class OrcidSearchResponse
{
    public List<OrcidSearchEntry>? Result { get; set; }
}

public sealed class OrcidSearchEntry
{
    [JsonPropertyName("orcid-identifier")]
    public OrcidIdentifier? OrcidIdentifier { get; set; }
}

public sealed class OrcidIdentifier
{
    public string? Path { get; set; }
}
