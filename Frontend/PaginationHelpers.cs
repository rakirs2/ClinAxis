namespace Frontend;

internal static class PaginationHelpers
{
    public static List<int?> GetPaginationRange(int current, int total, int maxVisible = 10)
    {
        var range = new List<int?>();
        if (total <= maxVisible)
        {
            for (int i = 1; i <= total; i++) range.Add(i);
            return range;
        }

        int half = maxVisible / 2;
        int start = Math.Max(1, current - half);
        int end = Math.Min(total, start + maxVisible - 1);

        if (end - start + 1 < maxVisible)
            start = Math.Max(1, end - maxVisible + 1);

        if (start > 1)
        {
            range.Add(1);
            if (start > 2) range.Add(null);
        }

        for (int i = start; i <= end; i++)
            range.Add(i);

        if (end < total)
        {
            if (end < total - 1) range.Add(null);
            range.Add(total);
        }

        return range;
    }
}
