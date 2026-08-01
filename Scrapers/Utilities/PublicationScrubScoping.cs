using Scrapers.Persistence.Entities;

namespace Scrapers.Utilities;

/// <summary>
/// Pure helpers for investigator publication scrubbing (issue #334).
/// </summary>
public static class PublicationScrubScoping
{
    /// <summary>
    /// Returns the distinct non-whitespace PMIDs from references belonging to any of the person's studies.
    /// A person is only linked to papers cited by their own studies — never to papers cited by other studies.
    /// </summary>
    public static IReadOnlyList<string> CandidatePmidsForPersonStudies(
        IReadOnlySet<string> personStudyNctIds,
        IEnumerable<(string StudyNctId, string? Pmid)> references)
    {
        ArgumentNullException.ThrowIfNull(personStudyNctIds);
        ArgumentNullException.ThrowIfNull(references);

        if (personStudyNctIds.Count == 0)
        {
            return [];
        }

        return references
            .Where(r => personStudyNctIds.Contains(r.StudyNctId) && !string.IsNullOrWhiteSpace(r.Pmid))
            .Select(r => r.Pmid!)
            .Distinct()
            .ToList();
    }
}
