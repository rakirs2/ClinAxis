using System;

namespace Scrapers.Persistence.Entities;

/// <summary>
/// Represents a single anonymous page view. Stores no identifying data:
/// visitors are identified only by a random session GUID, never by IP address.
/// </summary>
public sealed class PageViewEntity
{
    public int Id { get; set; }

    /// <summary>Path visited (e.g. "/status").</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Anonymized visitor session identifier (random GUID).</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>When the view occurred (UTC).</summary>
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}