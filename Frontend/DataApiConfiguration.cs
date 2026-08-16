namespace Frontend;

internal static class DataApiConfiguration
{
    internal const string DefaultBaseUrl = "http://127.0.0.1:5003";

    internal static string ResolveBaseUrl(string? configuredBaseUrl)
    {
        return string.IsNullOrWhiteSpace(configuredBaseUrl) ? DefaultBaseUrl : configuredBaseUrl;
    }
}
