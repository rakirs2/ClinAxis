using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;

namespace DataApi;

internal sealed class MeshTreeStore
{
    private readonly string _connectionString;
    private List<DescriptorInfo> _descriptors = [];
    private Dictionary<int, int> _studyCounts = [];
    private Dictionary<string, List<int>> _synonymLookup = new();
    private readonly object _lock = new();

    internal record DescriptorInfo(int Id, string Name, string[] TreeNumbers, string Category);

    private record TreeCacheEntry(object Result, DateTime CachedAt);
    private readonly ConcurrentDictionary<string, TreeCacheEntry> _treeCache = new();
    private readonly ConcurrentDictionary<string, byte> _refreshInProgress = new();
    private readonly TimeSpan _cacheTtl;

    public MeshTreeStore(string connectionString) : this(connectionString, TimeSpan.FromMinutes(10)) { }

    internal MeshTreeStore(string connectionString, TimeSpan cacheTtl)
    {
        _connectionString = connectionString;
        _cacheTtl = cacheTtl;
    }

    public async Task InitializeAsync()
    {
        using var ctx = CreateContext();
        var list = await ctx.MeshDescriptors
            .Select(m => new DescriptorInfo(m.Id, m.Name ?? "", m.TreeNumbers.ToArray(), m.Category ?? ""))
            .ToListAsync()
            .ConfigureAwait(false);
        lock (_lock) { _descriptors = list; }
    }

    public async Task LoadSynonymsAsync(string meshTermsJsonPath)
    {
        if (!File.Exists(meshTermsJsonPath))
        {
            await Console.Out.WriteLineAsync("  [MeSH] mesh_terms.json not found, skipping synonym load");
            return;
        }

        var json = await File.ReadAllTextAsync(meshTermsJsonPath).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.GetProperty("names").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        var cuis = doc.RootElement.GetProperty("cuis").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();

        var seenCuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cuiToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            foreach (var d in _descriptors)
            {
                var key = d.Name.Trim().ToUpperInvariant();
                if (!_synonymLookup.ContainsKey(key))
                    _synonymLookup[key] = [];
                _synonymLookup[key].Add(d.Id);
            }
        }

        var canonicalCuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        lock (_lock)
        {
            foreach (var d in _descriptors)
                canonicalCuis.Add(d.Name.Trim());
        }

        for (int i = 0; i < names.Length; i++)
        {
            var cui = i < cuis.Length ? cuis[i] : "";
            if (string.IsNullOrEmpty(cui)) continue;
            if (!seenCuis.Add(cui)) continue;
            var canonicalName = names[i].Trim();
            var canonicalKey = canonicalName.ToUpperInvariant();
            if (_synonymLookup.TryGetValue(canonicalKey, out var ids) && ids.Count > 0)
            {
                if (!cuiToId.ContainsKey(cui))
                    cuiToId[cui] = ids[0];
            }
        }

        seenCuis.Clear();
        for (int i = 0; i < names.Length; i++)
        {
            var cui = i < cuis.Length ? cuis[i] : "";
            if (string.IsNullOrEmpty(cui)) continue;
            if (!seenCuis.Add(cui)) continue;

            if (!cuiToId.TryGetValue(cui, out var descriptorId)) continue;

            lock (_lock)
            {
                var synonymKey = names[i].Trim().ToUpperInvariant();
                if (!_synonymLookup.ContainsKey(synonymKey))
                    _synonymLookup[synonymKey] = [];
                if (!_synonymLookup[synonymKey].Contains(descriptorId))
                    _synonymLookup[synonymKey].Add(descriptorId);
            }
        }

        await Console.Out.WriteLineAsync($"  [MeSH] Loaded {_synonymLookup.Count} synonym entries for {cuiToId.Count} descriptors");
    }

    public List<SearchResult> SearchDescriptors(string q, string? branch, int maxResults)
    {
        var (descriptors, counts) = Snapshot();

        var queryWords = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matchedIds = new HashSet<int>();

        foreach (var d in descriptors)
        {
            if (branch != null && !d.TreeNumbers.Any(tn => tn.StartsWith(branch, StringComparison.Ordinal)))
                continue;

            if (d.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                matchedIds.Add(d.Id);
                continue;
            }

            if (WordMatch(d.Name, queryWords))
            {
                matchedIds.Add(d.Id);
                continue;
            }

            var key = d.Name.Trim().ToUpperInvariant();
            if (_synonymLookup.TryGetValue(key, out var synonymIds) && synonymIds.Contains(d.Id))
            {
                if (SynonymMatch(key, queryWords, q))
                    matchedIds.Add(d.Id);
            }
        }

        foreach (var (synonymKey, descriptorIds) in _synonymLookup)
        {
            if (!synonymKey.Contains(q, StringComparison.OrdinalIgnoreCase) && !WordMatch(synonymKey, queryWords))
                continue;

            foreach (var descId in descriptorIds)
            {
                var desc = descriptors.FirstOrDefault(d => d.Id == descId);
                if (desc == null) continue;

                if (branch != null && !desc.TreeNumbers.Any(tn => tn.StartsWith(branch, StringComparison.Ordinal)))
                    continue;

                matchedIds.Add(descId);
            }
        }

        var results = descriptors
            .Where(d => matchedIds.Contains(d.Id))
            .OrderByDescending(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0)
            .ThenBy(d => d.Name)
            .Take(maxResults)
            .Select(d => new SearchResult
            {
                DescriptorId = d.Id,
                Name = d.Name,
                TreeNumber = d.TreeNumbers.FirstOrDefault() ?? "",
                StudyCount = counts.TryGetValue(d.Id, out var cnt) ? cnt : 0,
            })
            .ToList();

        return results;
    }

    internal sealed class SearchResult
    {
        public int DescriptorId { get; set; }
        public string Name { get; set; } = "";
        public string TreeNumber { get; set; } = "";
        public int StudyCount { get; set; }
    }

    private static bool WordMatch(string name, string[] queryWords)
    {
        var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return queryWords.All(qw => nameWords.Any(nw => WordMatches(qw, nw)));
    }

    private static bool WordMatches(string queryWord, string nameWord)
    {
        if (nameWord.StartsWith(queryWord, StringComparison.OrdinalIgnoreCase))
            return true;
        if (queryWord.Length > 3 && queryWord.EndsWith('s'))
            return nameWord.StartsWith(queryWord[..^1], StringComparison.OrdinalIgnoreCase);
        if (queryWord.Length > 4 && queryWord.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            return nameWord.StartsWith(queryWord[..^2], StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static bool SynonymMatch(string synonymKey, string[] queryWords, string rawQuery)
    {
        if (synonymKey.Contains(rawQuery, StringComparison.OrdinalIgnoreCase))
            return true;
        return WordMatch(synonymKey, queryWords);
    }

    public async Task RefreshCountsAsync()
    {
        using var ctx = CreateContext();
        var counts = await ctx.StudyConditions
            .GroupBy(sc => sc.MeshDescriptorId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count)
            .ConfigureAwait(false);
        lock (_lock) { _studyCounts = counts; }
    }

    public (IReadOnlyList<DescriptorInfo> Descriptors, IReadOnlyDictionary<int, int> Counts) Snapshot()
    {
        lock (_lock)
        {
            return (_descriptors.ToList().AsReadOnly(), new Dictionary<int, int>(_studyCounts));
        }
    }

    internal void SetTestData(List<DescriptorInfo> descriptors, Dictionary<int, int> studyCounts)
    {
        lock (_lock)
        {
            _descriptors = descriptors;
            _studyCounts = studyCounts;
            _synonymLookup = new Dictionary<string, List<int>>();
        }
    }

    internal void SetTestSynonyms(Dictionary<string, List<int>> synonyms)
    {
        lock (_lock)
        {
            _synonymLookup = synonyms;
        }
    }

    public object GetOrBuildTree(string key, Func<object> builder)
    {
        if (_treeCache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.CachedAt < _cacheTtl)
                return entry.Result;

            if (_refreshInProgress.TryAdd(key, 0))
            {
                var staleEntry = entry;
                _ = Task.Run(() =>
                {
                    try
                    {
                        var result = builder();
                        if (_treeCache.TryGetValue(key, out var current) && current == staleEntry)
                            _treeCache[key] = new(result, DateTime.UtcNow);
                    }
                    finally
                    {
                        _refreshInProgress.TryRemove(key, out _);
                    }
                });
            }
            return entry.Result;
        }

        var computed = builder();
        _treeCache[key] = new(computed, DateTime.UtcNow);
        return computed;
    }

    public object BuildTree(string? branch, int minStudyCount, int depth)
    {
        var (descriptors, counts) = Snapshot();

        if (string.IsNullOrEmpty(branch))
        {
            var categories = new (string Prefix, string Name)[]
            {
                ("A", "Anatomy"),
                ("B", "Organisms"),
                ("C", "Diseases"),
                ("D", "Chemicals and Drugs"),
                ("E", "Analytical, Diagnostic and Therapeutic Techniques and Equipment"),
                ("F", "Psychiatry and Psychology"),
                ("G", "Phenomena and Processes"),
                ("H", "Disciplines and Occupations"),
                ("I", "Anthropology, Education, Sociology and Social Phenomena"),
                ("J", "Technology, Industry, Agriculture"),
                ("K", "Humanities"),
                ("L", "Information Science"),
                ("M", "Named Groups"),
                ("N", "Health Care"),
                ("V", "Publication Characteristics"),
                ("Z", "Geographicals"),
            };

            var rootNodes = categories
                .Select(c =>
                {
                    var studyCount = descriptors
                        .Where(d => d.TreeNumbers.Any(tn => tn.StartsWith(c.Prefix, StringComparison.Ordinal)))
                        .Sum(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0);
                    var hasChildren = descriptors
                        .Any(d => d.TreeNumbers.Any(tn => tn.StartsWith(c.Prefix, StringComparison.Ordinal) && tn.Length > 1));
                    var children = depth > 1 && hasChildren
                        ? GetBranchNodes(descriptors, counts, c.Prefix, minStudyCount, depth - 1).ToArray()
                        : null;
                    return new
                    {
                        treeNumber = c.Prefix,
                        name = c.Name,
                        studyCount,
                        hasChildren,
                        children,
                    };
                })
                .Where(n => n.studyCount >= minStudyCount)
                .ToList();

            return new { branch = "__root__", nodes = rootNodes };
        }

        var nodes = GetBranchNodes(descriptors, counts, branch!, minStudyCount, depth);
        return new { branch, nodes };
    }

    private static List<object> GetBranchNodes(
        IReadOnlyList<DescriptorInfo> descriptors,
        IReadOnlyDictionary<int, int> counts,
        string currentBranch,
        int minStudyCount,
        int remainingDepth)
    {
        var isTopLevel = currentBranch.Length == 1;
        var prefix = isTopLevel ? currentBranch : currentBranch + ".";
        var childBranches = descriptors
            .SelectMany(d => d.TreeNumbers)
            .Where(tn => tn.StartsWith(prefix, StringComparison.Ordinal))
            .Select(tn =>
            {
                var remainder = tn[prefix.Length..];
                var dotIdx = remainder.IndexOf('.', StringComparison.Ordinal);
                return dotIdx > 0 ? tn[..(prefix.Length + dotIdx)] : tn;
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x)
            .ToList();

        var results = new List<object>();
        foreach (var childTn in childBranches)
        {
            var matchingDescriptors = descriptors
                .Where(d => d.TreeNumbers.Any(tn =>
                    tn.Equals(childTn, StringComparison.Ordinal) ||
                    tn.StartsWith(childTn + ".", StringComparison.Ordinal)))
                .ToList();

            var studyCount = matchingDescriptors.Sum(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0);
            if (studyCount < minStudyCount)
                continue;

            var desc = matchingDescriptors
                .OrderByDescending(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0)
                .FirstOrDefault();
            var hasChildren = descriptors.Any(d => d.TreeNumbers.Any(tn =>
                tn.StartsWith(childTn + ".", StringComparison.Ordinal) &&
                (counts.TryGetValue(d.Id, out var cnt) ? cnt : 0) > 0));

            object[]? children = null;
            if (remainingDepth > 1 && hasChildren)
            {
                children = GetBranchNodes(descriptors, counts, childTn, minStudyCount, remainingDepth - 1).ToArray();
            }

            results.Add(new
            {
                treeNumber = childTn,
                name = desc?.Name ?? childTn,
                studyCount,
                hasChildren,
                children,
            });
        }
        return results;
    }

    private ClinicalTrialsContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        builder.ConfigureNpgsql(_connectionString);
        return new ClinicalTrialsContext(builder.Options);
    }
}
