using Scrapers.Persistence.Entities;

namespace Scrapers.Utilities;

public sealed record RejectedNameStats(string? Reason, int Occurrences, int Studies);

public sealed record RejectedNameSyncPlan(
    IReadOnlyList<RejectedInvestigatorNameEntity> ToAdd,
    IReadOnlyList<RejectedInvestigatorNameEntity> ToUpdate,
    IReadOnlyList<RejectedInvestigatorNameEntity> ToRemove);

/// <summary>
/// Pure reconciliation logic for <c>rejected_investigator_names</c>.
/// An overridden name (IsHumanOverride == true) is authoritative: it is never
/// re-added as rejected, never re-scored, and its row is never purged, so the
/// reviewer's note and flag survive future ingest and validation runs.
/// </summary>
public static class RejectedNameSync
{
    public static RejectedNameSyncPlan Plan(
        IReadOnlyCollection<RejectedInvestigatorNameEntity> existing,
        IReadOnlyDictionary<string, RejectedNameStats> currentlyFailing,
        IReadOnlyCollection<string> overriddenNames)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(currentlyFailing);
        ArgumentNullException.ThrowIfNull(overriddenNames);

        var existingByName = existing.ToDictionary(e => e.FullName, StringComparer.OrdinalIgnoreCase);
        var overridden = overriddenNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        var toAdd = new List<RejectedInvestigatorNameEntity>();
        var toUpdate = new List<RejectedInvestigatorNameEntity>();

        foreach (var (name, stats) in currentlyFailing)
        {
            if (overridden.Contains(name))
            {
                continue;
            }

            if (existingByName.TryGetValue(name, out var entity))
            {
                entity.OccurrenceCount = stats.Occurrences;
                entity.StudyCount = stats.Studies;
                entity.RejectionReason = stats.Reason;
                entity.UpdatedAt = now;
                toUpdate.Add(entity);
            }
            else
            {
                toAdd.Add(new RejectedInvestigatorNameEntity
                {
                    FullName = name,
                    OccurrenceCount = stats.Occurrences,
                    StudyCount = stats.Studies,
                    RejectionReason = stats.Reason,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        var toRemove = existing
            .Where(e => e.IsHumanOverride != true && !currentlyFailing.ContainsKey(e.FullName))
            .ToList();

        return new RejectedNameSyncPlan(toAdd, toUpdate, toRemove);
    }
}
