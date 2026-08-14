namespace Frontend;

internal static class SearchQueryBuilder
{
    public static string BuildStudiesQuery(int page, int pageSize, SearchCriteria criteria)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrEmpty(criteria.Keyword))
            queryParams.Add($"keyword={Uri.EscapeDataString(criteria.Keyword)}");

        if (criteria.Statuses.Count > 0)
            queryParams.Add($"status={Uri.EscapeDataString(string.Join(",", criteria.Statuses))}");

        if (criteria.Phases.Count > 0)
            queryParams.Add($"phase={Uri.EscapeDataString(string.Join(",", criteria.Phases))}");

        if (criteria.MeshTreePrefixes.Count > 0)
            queryParams.Add($"meshTree={Uri.EscapeDataString(string.Join(",", criteria.MeshTreePrefixes))}");

        if (criteria.Conditions.Count > 0)
            queryParams.Add($"condition={Uri.EscapeDataString(string.Join(",", criteria.Conditions))}");

        if (criteria.EnrollmentMin.HasValue)
            queryParams.Add($"enrollmentMin={criteria.EnrollmentMin}");

        if (criteria.EnrollmentMax.HasValue)
            queryParams.Add($"enrollmentMax={criteria.EnrollmentMax}");

        if (criteria.StartDateFrom.HasValue)
            queryParams.Add($"startDateFrom={criteria.StartDateFrom.Value:yyyy-MM-dd}");

        if (criteria.StartDateTo.HasValue)
            queryParams.Add($"startDateTo={criteria.StartDateTo.Value:yyyy-MM-dd}");

        return string.Join("&", queryParams);
    }
}
