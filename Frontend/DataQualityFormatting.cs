using System.Globalization;

namespace Frontend;

internal static class DataQualityFormatting
{
    public static string FormatDate(DateTime dt) =>
        dt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public static string TruncatePayload(string? data)
    {
        if (string.IsNullOrEmpty(data)) return "--";
        return data.Length > 50 ? data[..50] + "..." : data;
    }

    public static string TruncateText(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length > maxLen ? text[..maxLen] + "..." : text;
    }

    public static int GetCount(IReadOnlyDictionary<string, int>? breakdown, string key) =>
        breakdown is not null && breakdown.TryGetValue(key, out var count) ? count : 0;
}
