namespace Frontend;

internal static class StatusFormatting
{
    public static string FormatDuration(double? ms)
    {
        if (ms is not > 0)
            return "--";
        var totalSeconds = (int)(ms.Value / 1000);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return minutes > 0 ? $"~{minutes}m {seconds}s" : $"~{seconds}s";
    }
}
