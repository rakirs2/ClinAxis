namespace DataApi.Endpoints;

internal static class EndpointHelpers
{
    internal static IReadOnlyList<string>? ParseCsvParam(string? param)
    {
        if (string.IsNullOrWhiteSpace(param))
            return null;

        return param.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
