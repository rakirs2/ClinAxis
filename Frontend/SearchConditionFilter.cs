namespace Frontend;

internal static class SearchConditionFilter
{
    public static List<string> Filter(IEnumerable<string>? all, string text)
    {
        if (string.IsNullOrEmpty(text) || all is null)
        {
            return [];
        }

        return all
            .Where(c => c.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();
    }
}
