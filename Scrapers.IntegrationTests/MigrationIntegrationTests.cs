using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        var tables = await ctx.Database.SqlQuery<string>(
            $"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync();

        Assert.IsTrue(tables.Contains("studies"), "Expected 'studies' table");
        Assert.IsTrue(tables.Contains("investigator_persons"), "Expected 'investigator_persons' table");
        Assert.IsTrue(tables.Contains("__EFMigrationsHistory"), "Expected migrations history table");
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

        var pending = await ctx.Database.GetPendingMigrationsAsync();
        Assert.AreEqual(0, pending.Count(), "No migrations should be pending after applying all.");
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
}
