namespace Scrapers.Utilities;

/// <summary>
/// Splits a person's full name into the (first, last) combinations used for
/// NPPES lookups. Pure — no database access. Returns an empty list when the
/// name cannot yield a usable lookup (empty, whitespace, or all prefix/suffix
/// tokens), so callers never index into an empty token array.
/// </summary>
internal static class PersonNameParts
{
    /// <summary>
    /// "John A Smith" → [("John","Smith"), ("John","A Smith")] and, for a
    /// single-letter middle initial, [("John","Smith")] again — matching the
    /// enrichment service's historical variation strategy.
    /// </summary>
    public static IReadOnlyList<(string First, string Last)> Variations(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return [];
        }

        var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 1 ? nameParts[0] : "";
        var lastName = nameParts.Length > 1 ? nameParts[^1] : nameParts[0];

        var variations = new List<(string First, string Last)>
        {
            (firstName, lastName)
        };

        if (nameParts.Length > 2)
        {
            variations.Add((nameParts[0], string.Join(" ", nameParts[1..])));
            if (nameParts.Length == 3 && nameParts[1].Length <= 2)
            {
                variations.Add((nameParts[0], nameParts[^1]));
            }
        }

        return variations;
    }
}
