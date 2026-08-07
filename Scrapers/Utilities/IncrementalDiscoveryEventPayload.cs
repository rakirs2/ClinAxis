using System.Text.Json;

namespace Scrapers.Utilities;

/// <summary>
/// Payload for a ClinicalTrials.gov incremental discovery event. The upper bound
/// makes the counted window reproducible when the event is retried.
/// </summary>
public sealed record IncrementalDiscoveryEventPayload(
    int Count,
    DateTime? LastUpdatedPost,
    DateTime? LastUpdatedPostTo)
{
    public bool HasWindow => LastUpdatedPostTo.HasValue;

    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            count = Count,
            lastUpdatedPost = LastUpdatedPost,
            lastUpdatedPostTo = LastUpdatedPostTo
        });
    }

    /// <summary>
    /// Parses both the bounded payload written by current workers and legacy
    /// count-only payloads already persisted in production.
    /// </summary>
    public static bool TryParse(string? data, out IncrementalDiscoveryEventPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(data);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("count", out JsonElement countElement) ||
                !countElement.TryGetInt32(out int count) ||
                count <= 0)
            {
                return false;
            }

            if (!root.TryGetProperty("lastUpdatedPostTo", out JsonElement toElement))
            {
                payload = new IncrementalDiscoveryEventPayload(count, null, null);
                return true;
            }

            if (!toElement.TryGetDateTime(out DateTime lastUpdatedPostTo))
            {
                return false;
            }

            DateTime? lastUpdatedPost = null;
            if (root.TryGetProperty("lastUpdatedPost", out JsonElement fromElement) &&
                fromElement.ValueKind != JsonValueKind.Null)
            {
                if (!fromElement.TryGetDateTime(out DateTime parsedFrom))
                {
                    return false;
                }

                lastUpdatedPost = parsedFrom;
            }

            if (lastUpdatedPost.HasValue && lastUpdatedPost.Value > lastUpdatedPostTo)
            {
                return false;
            }

            payload = new IncrementalDiscoveryEventPayload(count, lastUpdatedPost, lastUpdatedPostTo);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
