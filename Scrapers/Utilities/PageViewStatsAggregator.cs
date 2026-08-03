using System;
using System.Collections.Generic;
using System.Linq;
using Scrapers.Persistence.Entities;

namespace Scrapers.Utilities;

/// <summary>Anonymized visitor statistics aggregated from page-view rows.</summary>
public sealed class PageViewStats
{
    public int TotalViews { get; init; }

    public int UniqueVisitors { get; init; }

    public IReadOnlyList<TopPage> TopPages { get; init; } = [];
}

/// <summary>Most-visited page within a period.</summary>
public sealed class TopPage
{
    public TopPage(string path, int count)
    {
        Path = path;
        Count = count;
    }

    public string Path { get; }

    public int Count { get; }
}

/// <summary>
/// Pure aggregation logic for page views: total views, unique visitor sessions, and top pages.
/// Calendar-based period windows (UTC): day = start of today, week = start of Monday, month = start of the month.
/// </summary>
public static class PageViewStatsAggregator
{
    public static PageViewStats Aggregate(IReadOnlyList<PageViewEntity> rows, int topPages = 10)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var totalViews = rows.Count;
        var uniqueVisitors = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.SessionId))
            .Select(r => r.SessionId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var top = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Path))
            .GroupBy(r => r.Path, StringComparer.Ordinal)
            .Select(g => new TopPage(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Path, StringComparer.Ordinal)
            .Take(topPages)
            .ToList();

        return new PageViewStats
        {
            TotalViews = totalViews,
            UniqueVisitors = uniqueVisitors,
            TopPages = top
        };
    }

    /// <summary>
    /// Parses a requested period ("day", "week", "month") into the UTC instant that starts the window.
    /// </summary>
    public static bool TryParsePeriod(string? period, DateTime nowUtc, out DateTime fromUtc)
    {
        switch (period?.Trim().ToLowerInvariant())
        {
            case "day":
                fromUtc = nowUtc.Date;
                return true;
            case "week":
            {
                int daysSinceMonday = ((int)nowUtc.DayOfWeek + 6) % 7;
                fromUtc = nowUtc.Date.AddDays(-daysSinceMonday);
                return true;
            }
            case "month":
                fromUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return true;
            default:
                fromUtc = default;
                return false;
        }
    }
}