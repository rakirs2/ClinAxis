using System.Globalization;
using System.Text.Json;

namespace Scrapers.Utilities;

/// <summary>
/// Payload of a <c>studies.backfill</c> queue event: one date-window chunk of the
/// full-corpus sweep. Dates are date-only (yyyy-MM-dd), matching the API's granularity;
/// <see cref="SweepStartedUtc"/> is the UTC stamp written on every study the chunk upserts,
/// so the sweep's completion reconciliation can detect studies CT.gov no longer has.
/// </summary>
public sealed record BackfillEventPayload(int Count, DateOnly DateFrom, DateOnly DateTo, DateTime SweepStartedUtc, int? ChunkIndex = null)
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Parses the JSON event payload. Returns false (with a null payload) for null,
    /// malformed, or incomplete data; a missing <c>chunkIndex</c> is allowed.
    /// </summary>
    public static bool TryParse(string? data, out BackfillEventPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("count", out JsonElement countElement) ||
                !root.TryGetProperty("dateFrom", out JsonElement fromElement) ||
                !root.TryGetProperty("dateTo", out JsonElement toElement) ||
                !root.TryGetProperty("sweepStartedUtc", out JsonElement sweepElement))
            {
                return false;
            }

            if (!countElement.TryGetInt32(out int count) || count <= 0)
            {
                return false;
            }

            if (!DateOnly.TryParseExact(fromElement.GetString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly from) ||
                !DateOnly.TryParseExact(toElement.GetString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly to))
            {
                return false;
            }

            if (from > to)
            {
                return false;
            }

            if (!sweepElement.TryGetDateTime(out DateTime sweepStartedUtc))
            {
                return false;
            }

            int? chunkIndex = null;
            if (root.TryGetProperty("chunkIndex", out JsonElement indexElement) && indexElement.TryGetInt32(out int parsedIndex))
            {
                chunkIndex = parsedIndex;
            }

            payload = new BackfillEventPayload(count, from, to, sweepStartedUtc, chunkIndex);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // TryGetInt32/TryGetDateTime throw when the element is not a JSON value.
            return false;
        }
    }
}
