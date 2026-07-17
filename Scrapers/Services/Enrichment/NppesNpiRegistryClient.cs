using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrapers.Services.Enrichment;

public sealed class NppesNpiRegistryClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public NppesNpiRegistryClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress ??= new Uri("https://npiregistry.cms.hhs.gov/");
    }

    public async Task<IReadOnlyList<NpiRegistryResult>> SearchByNameAsync(string firstName, string lastName, string? affiliation = null, string? state = null, CancellationToken ct = default)
    {
        var url = $"/api/?version=2.1&first_name={Uri.EscapeDataString(firstName)}&last_name={Uri.EscapeDataString(lastName)}&limit=20";
        if (!string.IsNullOrWhiteSpace(state))
            url += $"&state={Uri.EscapeDataString(state)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<NpiSearchResponse>(json, _jsonOptions, ct).ConfigureAwait(false);

        var results = envelope?.Results ?? [];
        if (!string.IsNullOrWhiteSpace(affiliation) && results.Count > 1)
        {
            var orgResults = results.Where(r => r.Basic?.OrganizationName != null &&
                r.Basic.OrganizationName.Contains(affiliation, StringComparison.OrdinalIgnoreCase)).ToList();
            if (orgResults.Count > 0)
                return orgResults;
        }
        return results;
    }

    public async Task<IReadOnlyList<NpiRegistryResult>> SearchByOrganizationAsync(string organization, string? state = null, CancellationToken ct = default)
    {
        var url = $"/api/?version=2.1&organization_name={Uri.EscapeDataString(organization)}&limit=5";
        if (!string.IsNullOrWhiteSpace(state))
            url += $"&state={Uri.EscapeDataString(state)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<NpiSearchResponse>(json, _jsonOptions, ct).ConfigureAwait(false);

        return envelope?.Results ?? [];
    }
}

public sealed class NpiSearchResponse
{
    [JsonPropertyName("result_count")]
    public int ResultCount { get; set; }
    public List<NpiRegistryResult>? Results { get; set; }
}

public sealed class NpiRegistryResult
{
    public string? Number { get; set; }
    public NpiBasic? Basic { get; set; }
    public IReadOnlyList<NpiAddress>? Addresses { get; set; }
    public IReadOnlyList<NpiTaxonomy>? Taxonomies { get; set; }
    public IReadOnlyList<NpiIdentifier>? Identifiers { get; set; }
    public string? Status { get; set; }
    [JsonPropertyName("deactivation_date")]
    public DateTime? DeactivationDate { get; set; }
    [JsonPropertyName("reactivation_date")]
    public DateTime? ReactivationDate { get; set; }
}

public sealed class NpiIdentifier
{
    public string? Identifier { get; set; }
    [JsonPropertyName("identifier_type")]
    public string? IdentifierType { get; set; }
    public string? State { get; set; }
    public string? Issuer { get; set; }
}

public sealed class NpiBasic
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
    public string? Credential { get; set; }
    public string? Gender { get; set; }
    [JsonPropertyName("name_prefix")]
    public string? NamePrefix { get; set; }
    [JsonPropertyName("middle_name")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("organization_name")]
    public string? OrganizationName { get; set; }
}

public sealed class NpiAddress
{
    [JsonPropertyName("address_purpose")]
    public string? AddressPurpose { get; set; }
    [JsonPropertyName("address_1")]
    public string? Address1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }
}

public sealed class NpiTaxonomy
{
    public string? Code { get; set; }
    public string? Desc { get; set; }
    public bool Primary { get; set; }
    public string? State { get; set; }
    public string? License { get; set; }
}
