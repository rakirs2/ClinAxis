namespace Frontend;

/// <summary>
/// Parses the shareable search URL (the query string produced by
/// <see cref="SearchQueryBuilder.BuildStudiesQuery"/>) back into a
/// <see cref="SearchCriteria"/> + page number. Pure and DB-free so it can be
/// unit-tested; paired with SearchQueryBuilder it gives full URL round-trips.
/// </summary>
internal static class SearchUrlParser
{
    public static (SearchCriteria Criteria, int Page) Parse(string uri)
    {
        var criteria = new SearchCriteria();
        var page = 1;

        var queryParts = uri.Split('?', 2);
        if (queryParts.Length < 2)
        {
            return (criteria, page);
        }

        foreach (var pair in queryParts[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = pair.Split('=', 2);
            var key = keyValue[0];
            var value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : string.Empty;

            switch (key)
            {
                case "keyword":
                    criteria.Keyword = string.IsNullOrEmpty(value) ? null : value;
                    break;
                case "status":
                    criteria.Statuses = SplitCsv(value);
                    break;
                case "phase":
                    criteria.Phases = SplitCsv(value);
                    break;
                case "condition":
                    criteria.Conditions = SplitCsv(value);
                    break;
                case "meshTree":
                    criteria.MeshTreePrefixes = SplitCsv(value);
                    break;
                case "enrollmentMin":
                    criteria.EnrollmentMin = TryParseInt(value);
                    break;
                case "enrollmentMax":
                    criteria.EnrollmentMax = TryParseInt(value);
                    break;
                case "startDateFrom":
                    criteria.StartDateFrom = TryParseDate(value);
                    break;
                case "startDateTo":
                    criteria.StartDateTo = TryParseDate(value);
                    break;
                case "page":
                    var parsed = TryParseInt(value);
                    page = parsed is > 0 ? parsed.Value : 1;
                    break;
            }
        }

        return (criteria, page);
    }

    private static List<string> SplitCsv(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static int? TryParseInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static DateTime? TryParseDate(string value)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
