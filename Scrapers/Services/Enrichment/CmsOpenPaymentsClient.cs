using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrapers.Services.Enrichment;

public sealed class OpenPaymentRecord
{
    [JsonPropertyName("covered_recipient_npi")]
    public string? RecipientNpi { get; set; }

    [JsonPropertyName("covered_recipient_first_name")]
    public string? RecipientFirstName { get; set; }

    [JsonPropertyName("covered_recipient_last_name")]
    public string? RecipientLastName { get; set; }

    public string? PhysicianName => $"{RecipientFirstName} {RecipientLastName}".Trim();

    [JsonPropertyName("principal_investigator_1_npi")]
    public string? Pi1Npi { get; set; }

    [JsonPropertyName("principal_investigator_1_first_name")]
    public string? Pi1FirstName { get; set; }

    [JsonPropertyName("principal_investigator_1_last_name")]
    public string? Pi1LastName { get; set; }

    [JsonPropertyName("total_amount_of_payment_usdollars")]
    public decimal? PaymentAmount { get; set; }

    [JsonPropertyName("date_of_payment")]
    public string? PaymentDateString { get; set; }

    [JsonPropertyName("form_of_payment_or_transfer_of_value")]
    public string? FormOfPayment { get; set; }

    [JsonPropertyName("nature_of_payment_or_transfer_of_value")]
    public string? NatureOfPayment { get; set; }

    [JsonPropertyName("applicable_manufacturer_or_applicable_gpo_making_payment_name")]
    public string? PayorName { get; set; }

    [JsonPropertyName("name_of_study")]
    public string? StudyName { get; set; }

    [JsonPropertyName("clinicaltrials_gov_identifier")]
    public string? ClinicalTrialsId { get; set; }

    [JsonPropertyName("context_of_research")]
    public string? ContextOfResearch { get; set; }

    [JsonPropertyName("product_category_or_therapeutic_area_1")]
    public string? ProductCategory { get; set; }

    [JsonPropertyName("name_of_drug_or_biological_or_device_or_medical_supply_1")]
    public string? ProductName { get; set; }

    [JsonPropertyName("program_year")]
    public string? ProgramYear { get; set; }

    [JsonPropertyName("record_id")]
    public string? RecordId { get; set; }
}

internal sealed class SocrataQueryResponse
{
    public List<OpenPaymentRecord>? Results { get; set; }
    public long? Count { get; set; }
}

public sealed class CmsOpenPaymentsClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://openpaymentsdata.cms.gov/api/1/datastore/query";
    private const int PageSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<string, string> ResearchUuids = new()
    {
        ["2019"] = "713a6016-1930-4a0c-b7c2-3e3de4f244c5",
        ["2020"] = "9c248e7e-7c7f-478b-ab84-ce0919d72c1c",
        ["2021"] = "ce1d28dd-0094-5060-a036-580329439600",
        ["2022"] = "fdc3c773-018a-412c-8a81-d7b8a13a037b",
        ["2023"] = "ec9521bf-9d97-4603-814c-f4132d34bc4f",
        ["2024"] = "2f15cb85-8887-4dcc-a318-1f8ec1d815b3",
        ["2025"] = "f0d1de67-6852-4093-a036-c9328c256a05"
    };

    private static readonly Dictionary<string, string> GeneralUuids = new()
    {
        ["2019"] = "4e54dd6c-30f8-4f86-86a7-3c109a89528e",
        ["2020"] = "a08c4b30-5cf3-4948-ad40-36f404619019",
        ["2021"] = "0380bbeb-aea1-58b6-b708-829f92a48202",
        ["2022"] = "df01c2f8-dc1f-4e79-96cb-8208beaf143c",
        ["2023"] = "fb3a65aa-c901-4a38-a813-b04b00dfa2a9",
        ["2024"] = "e6b17c6a-2534-4207-a4a1-6746a14911ff",
        ["2025"] = "fb0b1734-1410-429d-92f6-3f4b35218e5e"
    };

    private static readonly Dictionary<string, string> OwnershipUuids = new()
    {
        ["2019"] = "0b5c9710-7edb-484e-abc8-39293849ccb2",
        ["2020"] = "a9a0bf48-6b96-4589-b4c2-3c5dcfbeaca2",
        ["2021"] = "b0c03b8d-06df-58f2-8ce2-4daeffee147e",
        ["2022"] = "37792388-800f-427a-9e02-b11601454eeb",
        ["2023"] = "ac0bc85c-02e3-45d9-89e8-2ff43da85df7",
        ["2024"] = "9ac4f7f8-b6e4-4d80-8410-4aba7e71dd02",
        ["2025"] = "800aed1b-20ed-4d19-b0c9-dcd10f197ffc"
    };

    public CmsOpenPaymentsClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private async Task<List<OpenPaymentRecord>> FetchByNpiAsync(string uuid, string npi, CancellationToken ct)
    {
        var results = new List<OpenPaymentRecord>();
        int offset = 0;

        while (true)
        {
            var url = $"{BaseUrl}/{uuid}/0?limit={PageSize}&offset={offset}" +
                      "&count=true&results=true&format=json&keys=true" +
                      $"&conditions[0][property]=covered_recipient_npi" +
                      $"&conditions[0][value]={Uri.EscapeDataString(npi)}" +
                      "&conditions[0][operator]=";

            using var response = await _httpClient.GetAsync(new Uri(url), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                break;

            var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var queryResp = await JsonSerializer.DeserializeAsync<SocrataQueryResponse>(body, JsonOptions, ct).ConfigureAwait(false);

            if (queryResp?.Results == null || queryResp.Results.Count == 0)
                break;

            results.AddRange(queryResp.Results);
            offset += PageSize;

            if (offset >= (queryResp.Count ?? 0))
                break;
        }

        return results;
    }

    public async Task<List<OpenPaymentRecord>> GetResearchPaymentsByNpiAsync(string npi, string? year = null, CancellationToken ct = default)
    {
        return await FetchPaymentsByNpiAsync(ResearchUuids, npi, year, ct).ConfigureAwait(false);
    }

    public async Task<List<OpenPaymentRecord>> GetGeneralPaymentsByNpiAsync(string npi, string? year = null, CancellationToken ct = default)
    {
        return await FetchPaymentsByNpiAsync(GeneralUuids, npi, year, ct).ConfigureAwait(false);
    }

    public async Task<List<OpenPaymentRecord>> GetOwnershipByNpiAsync(string npi, string? year = null, CancellationToken ct = default)
    {
        return await FetchPaymentsByNpiAsync(OwnershipUuids, npi, year, ct).ConfigureAwait(false);
    }

    public async Task<List<OpenPaymentRecord>> GetAllPaymentsByNpiAsync(string npi, CancellationToken ct = default)
    {
        var results = new List<OpenPaymentRecord>();
        results.AddRange(await GetResearchPaymentsByNpiAsync(npi, null, ct).ConfigureAwait(false));
        results.AddRange(await GetGeneralPaymentsByNpiAsync(npi, null, ct).ConfigureAwait(false));
        results.AddRange(await GetOwnershipByNpiAsync(npi, null, ct).ConfigureAwait(false));
        return results;
    }

    private async Task<List<OpenPaymentRecord>> FetchPaymentsByNpiAsync(Dictionary<string, string> uuidMap, string npi, string? year, CancellationToken ct)
    {
        var years = year != null ? new[] { year } : (string[]?)null;
        var targetYears = years ?? [.. uuidMap.Keys];

        var results = new List<OpenPaymentRecord>();
        foreach (var y in targetYears)
        {
            if (!uuidMap.TryGetValue(y, out var uuid))
                continue;

            var yearResults = await FetchByNpiAsync(uuid, npi, ct).ConfigureAwait(false);
            results.AddRange(yearResults);
        }

        return results;
    }
}
