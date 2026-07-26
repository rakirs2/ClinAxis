using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;

namespace DataApi;

internal sealed class MeshTreeStore
{
    private readonly string _connectionString;
    private List<DescriptorInfo> _descriptors = [];
    private Dictionary<int, int> _studyCounts = [];
    private readonly object _lock = new();

    internal record DescriptorInfo(int Id, string Name, string[] TreeNumbers, string Category);

    public MeshTreeStore(string connectionString)
    {
        _connectionString = connectionString;
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

    private ClinicalTrialsContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        builder.ConfigureNpgsql(_connectionString);
        return new ClinicalTrialsContext(builder.Options);
    }
}
