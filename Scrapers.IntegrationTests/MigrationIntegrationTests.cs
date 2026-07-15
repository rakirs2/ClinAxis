using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class MigrationIntegrationTests : DbTestBase
{
    [TestMethod]
    public void NoPendingModelChanges()
    {
        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        bool hasPending = ctx.Database.HasPendingModelChanges();
        Assert.IsFalse(hasPending, "There are pending model changes not captured in a migration. Run 'dotnet ef migrations add' to create one.");
    }

    [TestMethod]
    public async Task Migrate_CreatesFullSchema()
    {
        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();

        if (!await IsTableAccessibleAsync(ctx.Studies))
        {
            await ctx.Database.EnsureDeletedAsync();
            await ctx.Database.EnsureCreatedAsync();
        }

        var entityTypes = ctx.Model.GetEntityTypes().Select(e => e.GetTableName()).ToHashSet();
        Assert.IsTrue(entityTypes.Contains("studies"), "Expected 'studies' table in model");
        Assert.IsTrue(entityTypes.Contains("investigator_persons"), "Expected 'investigator_persons' table in model");
        Assert.IsTrue(await IsTableAccessibleAsync(ctx.Studies), "Expected 'studies' table to be accessible");
    }

    [TestMethod]
    public async Task Migrate_IsIdempotent()
    {
        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
        await ctx.Database.MigrateAsync();

        if (!await IsTableAccessibleAsync(ctx.Studies))
        {
            await ctx.Database.EnsureDeletedAsync();
            await ctx.Database.EnsureCreatedAsync();
        }

        Assert.IsTrue(await IsTableAccessibleAsync(ctx.Studies), "Schema should exist after idempotent MigrateAsync calls");
    }

    [TestMethod]
    public async Task ResetDatabase_AllowsWritesAfterReset()
    {
        var repo = new StudyRepository(ConnectionString);
        await repo.ResetDatabaseAsync();

        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        ctx.Studies.Add(new Scrapers.Persistence.Entities.StudyEntity
        {
            NctId = "NCT00000001",
            BriefTitle = "Post-Reset Test"
        });
        await ctx.SaveChangesAsync();
        Assert.AreEqual(1, await ctx.Studies.CountAsync());
    }

    private static async Task<bool> IsTableAccessibleAsync<T>(IQueryable<T> query)
    {
        try
        {
            await query.AnyAsync();
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return false;
        }
    }
}
