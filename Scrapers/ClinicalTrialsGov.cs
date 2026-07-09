using System.Net;
using System.Text.Json;
using Scrapers.Models.ClinicalTrialsGov;

namespace Scrapers;

public class ClinicalTrialsGov
{
    private const string BaseUrl = "https://clinicaltrials.gov/api/v2/";
    private const string StudiesPath = "studies";
    private const int MaxPageSize = 100;
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan _initialBackoff = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly int _pageSize;
    private readonly Action<string>? _log;

    public ClinicalTrialsGov(HttpClient? httpClient = null, int pageSize = MaxPageSize, Action<string>? log = null)
    {
        if (pageSize is <= 0 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"pageSize must be between 1 and {MaxPageSize}.");
        }

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri(BaseUrl);

        _pageSize = pageSize;
        _log = log;
    }

    public async Task<IReadOnlyList<StudySummary>> GetTrialsAsync(int count = 500, CancellationToken cancellationToken = default)
    {
        return await GetTrialsInternalAsync(count, payload => payload.ToSummary(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClinicalTrialRecord>> GetTrialRecordsAsync(int count = 500, CancellationToken cancellationToken = default)
    {
        return await GetTrialsInternalAsync(count, payload => payload.ToRecord(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> GetTrialsInternalAsync<T>(int count, Func<StudyListResponse.StudyPayload, T?> projector, CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return Array.Empty<T>();
        }

        var collected = new List<T>(Math.Min(count, 1000));
        string? pageToken = null;

        while (collected.Count < count)
        {
            StudyListResponse response = await FetchPageAsync(pageToken, cancellationToken).ConfigureAwait(false);
            List<StudyListResponse.StudyPayload> studies = response.Studies ?? new List<StudyListResponse.StudyPayload>();

            foreach (StudyListResponse.StudyPayload studyPayload in studies)
            {
                T? item = projector(studyPayload);
                if (item is null)
                {
                    continue;
                }

                collected.Add(item);
                if (collected.Count >= count)
                {
                    break;
                }
            }

            if (collected.Count >= count || string.IsNullOrWhiteSpace(response.NextPageToken))
            {
                break;
            }

            pageToken = response.NextPageToken;
        }

        return collected;
    }

    private async Task<StudyListResponse> FetchPageAsync(string? pageToken, CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri(pageToken);
        TimeSpan delay = _initialBackoff;

        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage? response = null;

            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    StudyListResponse? payload = await JsonSerializer.DeserializeAsync<StudyListResponse>(stream, _serializerOptions, cancellationToken)
                        .ConfigureAwait(false);
                    return payload ?? new StudyListResponse();
                }

                if (!IsTransientStatus(response.StatusCode) || attempt == MaxRetryAttempts)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new HttpRequestException(
                        $"ClinicalTrials.gov returned {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
                }
            }
            catch (Exception ex) when (IsTransientException(ex, cancellationToken) && attempt < MaxRetryAttempts)
            {
                _log?.Invoke($"ClinicalTrials.gov request attempt {attempt} failed: {ex.Message}");
            }
            finally
            {
                response?.Dispose();
            }

            _log?.Invoke($"Retrying ClinicalTrials.gov request in {delay.TotalSeconds:F1}s (attempt {attempt}).");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
        }

        throw new InvalidOperationException("Unable to reach ClinicalTrials.gov after multiple attempts.");
    }

    private string BuildRequestUri(string? pageToken)
    {
        var query = $"?format=json&pageSize={_pageSize}";
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            query += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }

        return $"{StudiesPath}{query}";
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
        or (HttpStatusCode)429
        or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.GatewayTimeout;
    }

    private static bool IsTransientException(Exception exception, CancellationToken cancellationToken)
    {
        return (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested) && exception is HttpRequestException or TimeoutException or TaskCanceledException or OperationCanceledException;
    }
}
