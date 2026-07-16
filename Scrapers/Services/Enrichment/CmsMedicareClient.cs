using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrapers.Services.Enrichment;

/// <summary>
/// Custom JSON converter for nullable integers that gracefully handles
/// string values from the CMS Medicare API (which may return strings instead of numbers).
/// </summary>
internal sealed class FlexibleNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => 
                int.TryParse(reader.GetString(), out var value) ? value : null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing int?")
        };
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// Custom JSON converter for nullable longs that gracefully handles
/// string values from the CMS Medicare API.
/// </summary>
internal sealed class FlexibleNullableLongConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => 
                long.TryParse(reader.GetString(), out var value) ? value : null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing long?")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// Custom JSON converter for nullable decimals that gracefully handles
/// string values from the CMS Medicare API.
/// </summary>
internal sealed class FlexibleNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetDecimal(),
            JsonTokenType.String => 
                decimal.TryParse(reader.GetString(), out var value) ? value : null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing decimal?")
        };
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

public sealed class CmsMedicareClient
{
    private readonly HttpClient _httpClient;
    private readonly string _datasetUuid;
    
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new FlexibleNullableIntConverter());
        options.Converters.Add(new FlexibleNullableLongConverter());
        options.Converters.Add(new FlexibleNullableDecimalConverter());
        return options;
    }

    public CmsMedicareClient(HttpClient httpClient, string datasetUuid = "8889d81e-2ee7-448f-8713-f071038289b5")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _datasetUuid = datasetUuid;
    }

    public async Task<CmsMedicareRecord?> GetByNpiAsync(string npi, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(npi))
            return null;

        var url = $"{_datasetUuid}/data?filter[Rndrng_NPI][condition][value]={Uri.EscapeDataString(npi.Trim())}&size=5";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var records = await JsonSerializer.DeserializeAsync<List<CmsMedicareRecord>>(json, _jsonOptions, ct).ConfigureAwait(false);

        return records?.FirstOrDefault();
    }

    public async Task<IReadOnlyList<CmsMedicareRecord>> GetAllByNpiAsync(string npi, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(npi))
            return [];

        var url = $"{_datasetUuid}/data?filter[Rndrng_NPI][condition][value]={Uri.EscapeDataString(npi.Trim())}&size=20";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<List<CmsMedicareRecord>>(json, _jsonOptions, ct).ConfigureAwait(false) ?? [];
    }
}

public sealed class CmsMedicareRecord
{
    [JsonPropertyName("Rndrng_NPI")]
    public string? Npi { get; set; }

    [JsonPropertyName("Rndrng_Prvdr_Last_Org_Name")]
    public string? LastName { get; set; }

    [JsonPropertyName("Rndrng_Prvdr_First_Name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("Rndrng_Prvdr_Type")]
    public string? ProviderType { get; set; }

    [JsonPropertyName("Rndrng_Prvdr_Mdcr_Prtcptg_Ind")]
    public string? MedicareParticipationIndicator { get; set; }

    [JsonPropertyName("Tot_Benes")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? TotalBeneficiaries { get; set; }

    [JsonPropertyName("Tot_Srvcs")]
    [JsonConverter(typeof(FlexibleNullableLongConverter))]
    public long? TotalServices { get; set; }

    [JsonPropertyName("Tot_Sbmtd_Chrg")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? TotalSubmittedCharges { get; set; }

    [JsonPropertyName("Tot_Mdcr_Alowd_Amt")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? TotalMedicareAllowedAmount { get; set; }

    [JsonPropertyName("Tot_Mdcr_Pymt_Amt")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? TotalMedicarePaymentAmount { get; set; }

    [JsonPropertyName("Tot_Mdcr_Stdzd_Amt")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? TotalMedicareStandardizedAmount { get; set; }

    [JsonPropertyName("Bene_Age_LT_65_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneAgeLt65Count { get; set; }

    [JsonPropertyName("Bene_Age_65_74_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneAge65To74Count { get; set; }

    [JsonPropertyName("Bene_Age_75_84_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneAge75To84Count { get; set; }

    [JsonPropertyName("Bene_Age_GT_84_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneAgeGt84Count { get; set; }

    [JsonPropertyName("Bene_Feml_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneFemaleCount { get; set; }

    [JsonPropertyName("Bene_Male_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneMaleCount { get; set; }

    [JsonPropertyName("Bene_Dual_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneDualCount { get; set; }

    [JsonPropertyName("Bene_Ndual_Cnt")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? BeneNonDualCount { get; set; }

    [JsonPropertyName("Bene_Avg_Risk_Scre")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? AvgRiskScore { get; set; }

    [JsonPropertyName("Med_Tot_Srvcs")]
    [JsonConverter(typeof(FlexibleNullableLongConverter))]
    public long? MedicalServices { get; set; }

    [JsonPropertyName("Drug_Tot_Srvcs")]
    [JsonConverter(typeof(FlexibleNullableLongConverter))]
    public long? DrugServices { get; set; }

    [JsonPropertyName("Med_Mdcr_Pymt_Amt")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? MedicalMedicarePayment { get; set; }

    [JsonPropertyName("Drug_Mdcr_Pymt_Amt")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? DrugMedicarePayment { get; set; }

    [JsonPropertyName("Bene_CC_BH_ADHD_OthCD_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhAdhdOthCdPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Alcohol_Drug_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhAlcoholDrugPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Tobacco_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhTobaccoPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Alz_NonAlzdem_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhAlzNonAlzdemPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Anxiety_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhAnxietyPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Bipolar_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhBipolarPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Depress_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhDepressPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_PTSD_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhPtsdPct { get; set; }

    [JsonPropertyName("Bene_CC_BH_Schizo_OthPsy_V1_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcBhSchizoOthPsyPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Asthma_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhAsthmaPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Afib_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhAfibPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Cancer6_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhCancerPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_CKD_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhCkdPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_COPD_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhCopdPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Diabetes_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhDiabetesPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_HF_NonIHD_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhHfNonIhdPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Hyperlipidemia_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhHyperlipidemiaPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Hypertension_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhHypertensionPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_IschemicHeart_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhIschemicHeartPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Osteoporosis_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhOsteoporosisPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Parkinson_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhParkinsonPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Arthritis_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhArthritisPct { get; set; }

    [JsonPropertyName("Bene_CC_PH_Stroke_TIA_V2_Pct")]
    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? BeneCcPhStrokeTiaPct { get; set; }
}
