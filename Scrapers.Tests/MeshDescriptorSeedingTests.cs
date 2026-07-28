using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

internal static class MeshTestJsonOptions
{
    internal static readonly JsonSerializerOptions Instance = new() { PropertyNameCaseInsensitive = true };
}

[TestClass]
public sealed class MeshDescriptorSeedingTests : DbTestBase
{
    [TestMethod]
    public async Task SeedMeshDescriptorsAsync_CreatesTreePathRecords()
    {
        var jsonPath = MeshFixtureLoader.LoadMeshJson("test_mesh_terms.json");
        var repo = new StudyRepository(ConnectionString);

        await repo.SeedMeshDescriptorsAsync(jsonPath);

        using var ctx = CreateContext();
        var descriptorCount = await ctx.MeshDescriptors.CountAsync();
        var treePathCount = await ctx.Set<MeshTreePathEntity>().CountAsync();
        Assert.IsTrue(descriptorCount > 0);
        Assert.IsTrue(treePathCount > 0);
        Assert.IsTrue(treePathCount >= descriptorCount);

        var firstDesc = await ctx.MeshDescriptors.OrderBy(d => d.Id).FirstAsync();
        var firstPaths = await ctx.Set<MeshTreePathEntity>()
            .Where(tp => tp.MeshDescriptorId == firstDesc.Id)
            .ToListAsync();
        Assert.IsTrue(firstPaths.Count > 0);
        Assert.IsTrue(firstPaths.All(p => !string.IsNullOrEmpty(p.TreeNumber)));
    }

    [TestMethod]
    public async Task SeedMeshDescriptorsAsync_Idempotent_DoesNotDuplicate()
    {
        var jsonPath = MeshFixtureLoader.LoadMeshJson("test_mesh_terms.json");
        var repo = new StudyRepository(ConnectionString);

        await repo.SeedMeshDescriptorsAsync(jsonPath);

        using var ctx = CreateContext();
        var treePathCountAfterFirst = await ctx.Set<MeshTreePathEntity>().CountAsync();

        await repo.SeedMeshDescriptorsAsync(jsonPath);

        var treePathCountAfterSecond = await ctx.Set<MeshTreePathEntity>().CountAsync();
        Assert.AreEqual(treePathCountAfterFirst, treePathCountAfterSecond);
    }

    [TestMethod]
    public async Task BackfillTreePathsAsync_PopulatesMissingPaths()
    {
        var jsonPath = MeshFixtureLoader.LoadMeshJson("test_mesh_terms.json");
        var repo = new StudyRepository(ConnectionString);

        using (var ctx = CreateContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            var json = await File.ReadAllTextAsync(jsonPath);
            var data = System.Text.Json.JsonSerializer.Deserialize<MeshTermsFile>(json, MeshTestJsonOptions.Instance);
            var seenCuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < data!.Names!.Length; i++)
            {
                var cui = data.Cuis != null && i < data.Cuis.Length ? data.Cuis[i] : "";
                if (string.IsNullOrEmpty(cui) || !seenCuis.Add(cui)) continue;
                var tnArr = data.TreeNumbers != null && i < data.TreeNumbers.Length ? data.TreeNumbers[i] : null;
                var treeNumbers = tnArr?.Where(t => !string.IsNullOrEmpty(t)).ToArray() ?? [];
                ctx.MeshDescriptors.Add(new MeshDescriptorEntity
                {
                    Cui = cui,
                    Name = data.Names[i] ?? "",
                    TreeNumbers = treeNumbers,
                    Category = data.Categories != null && i < data.Categories.Length ? data.Categories[i] : "",
                });
            }
            await ctx.SaveChangesAsync();
        }

        using (var ctx = CreateContext())
        {
            var pathCountBefore = await ctx.Set<MeshTreePathEntity>().CountAsync();
            Assert.AreEqual(0, pathCountBefore);
        }

        await repo.BackfillTreePathsAsync();

        using (var ctx = CreateContext())
        {
            var pathCountAfter = await ctx.Set<MeshTreePathEntity>().CountAsync();
            Assert.IsTrue(pathCountAfter > 0);

            var descs = await ctx.MeshDescriptors.Where(d => d.TreeNumbers != null && d.TreeNumbers.Count > 0).ToListAsync();
            var expectedCount = descs.Sum(d => d.TreeNumbers.Count);
            Assert.AreEqual(expectedCount, pathCountAfter);
        }
    }

    [TestMethod]
    public async Task BackfillTreePathsAsync_Idempotent_SkipsWhenDataExists()
    {
        var jsonPath = MeshFixtureLoader.LoadMeshJson("test_mesh_terms.json");
        var repo = new StudyRepository(ConnectionString);

        await repo.SeedMeshDescriptorsAsync(jsonPath);

        var initialCount = await CreateContext().Set<MeshTreePathEntity>().CountAsync();

        await repo.BackfillTreePathsAsync();

        var afterCount = await CreateContext().Set<MeshTreePathEntity>().CountAsync();
        Assert.AreEqual(initialCount, afterCount);
    }

    private ClinicalTrialsContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        builder.ConfigureNpgsql(ConnectionString);
        return new ClinicalTrialsContext(builder.Options);
    }
}
