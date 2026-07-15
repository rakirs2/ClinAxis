using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Utilities;
using Testcontainers.PostgreSql;

var truncate = args.Contains("--truncate");
var studyLimit = args.Select(a => int.TryParse(a, out var n) ? n : (int?)null)
    .FirstOrDefault(n => n.HasValue) ?? 3000;

await Console.Out.WriteLineAsync($"=== Investigator Validation Pipeline (limit={studyLimit}) ===");

var externalConn = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
var useExternal = !string.IsNullOrEmpty(externalConn);

string? connectionString = null;
PostgreSqlContainer? container = null;

if (useExternal)
{
    connectionString = externalConn;
    await Console.Out.WriteLineAsync("Using external PostgreSQL (POSTGRES_CONNECTION_STRING set)");
}
else
{
    await Console.Out.WriteLineAsync("Starting PostgreSQL container...");
    container = new PostgreSqlBuilder()
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    await container.StartAsync();
    connectionString = container.GetConnectionString();
    await Console.Out.WriteLineAsync("Container ready.");
}

DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
    .UseNpgsql(connectionString).Options;

// Apply schema via migrations.
await Console.Out.WriteLineAsync("Applying schema...");
using (var ctx = new ClinicalTrialsContext(opts))
{
    if (truncate && useExternal)
    {
        await Console.Out.WriteLineAsync("Dropping all tables to reset schema...");
        // Dynamically drop ALL tables to handle schema drift from any previous model
        await ctx.Database.ExecuteSqlRawAsync(@"
            DO $$ DECLARE
                r RECORD;
            BEGIN
                FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = current_schema()) LOOP
                    EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
                END LOOP;
            END $$;
        ");
    }

    await ctx.Database.MigrateAsync();
}

var studyRepo = new StudyRepository(connectionString!);
var clinicalTrialsClient = new ClinicalTrialsGov();
var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);

await Console.Out.WriteLineAsync($"Scraping {studyLimit} studies from ClinicalTrials.gov...");
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var ingested = await clinicalTrialsIngestionService.IngestAsync(studyLimit);
stopwatch.Stop();
await Console.Out.WriteLineAsync($"Ingested {ingested} studies in {stopwatch.Elapsed.TotalSeconds:F1}s");

await Console.Out.WriteLineAsync("Scraping PubMed papers...");
stopwatch.Restart();
var pubMedScraperService = new PubMedScraperService(connectionString!);
var papersIngested = await pubMedScraperService.IngestPubMedPapersAsync();
stopwatch.Stop();
await Console.Out.WriteLineAsync($"Ingested {papersIngested} PubMed papers in {stopwatch.Elapsed.TotalSeconds:F1}s");

await Console.Out.WriteLineAsync("\n--- Results ---");
await Console.Out.WriteLineAsync($"Studies ingested: {ingested}");
await Console.Out.WriteLineAsync($"Pubmed papers ingested: {papersIngested}");

// Query ALL persons directly (bypass IsHuman filter to detect leaks)
using var context = new ClinicalTrialsContext(opts);
var allPersons = await context.InvestigatorPersons
    .Include(p => p.StudyInvestigators)
    .AsNoTracking()
    .ToListAsync();
var totalPersons = allPersons.Count;

var nonHumanCount = allPersons.Count(p => !p.IsHuman);
await Console.Out.WriteLineAsync($"Total investigator persons stored: {totalPersons}");
await Console.Out.WriteLineAsync($"  IsHuman=true:  {totalPersons - nonHumanCount}");
await Console.Out.WriteLineAsync($"  IsHuman=false: {nonHumanCount}");

var rejectedByFilter = new Dictionary<string, (string? Reason, int PersonCount, int StudyCount)>(StringComparer.OrdinalIgnoreCase);
var leaks = new List<(string Name, Guid Id, int StudyCount, bool IsHuman)>();
foreach (var person in allPersons)
{
    var result = NameFilter.IsHumanName(person.FullName, null);
    if (!result.IsHuman)
    {
        leaks.Add((person.FullName, person.Id, person.StudyInvestigators?.Count ?? 0, person.IsHuman));

        var key = person.FullName;
        if (rejectedByFilter.TryGetValue(key, out var existing))
        {
            rejectedByFilter[key] = (result.RejectionReason, existing.PersonCount + 1, existing.StudyCount + (person.StudyInvestigators?.Count ?? 0));
        }
        else
        {
            rejectedByFilter[key] = (result.RejectionReason, 1, person.StudyInvestigators?.Count ?? 0);
        }
    }
}

// Upsert rejected names into database
using (var upsertCtx = new ClinicalTrialsContext(opts))
{
    var existingRejected = await upsertCtx.Set<RejectedInvestigatorNameEntity>().ToListAsync();
    var existingByName = new Dictionary<string, RejectedInvestigatorNameEntity>(StringComparer.OrdinalIgnoreCase);
    foreach (var r in existingRejected)
    {
        existingByName[r.FullName] = r;
    }

    foreach (var kvp in rejectedByFilter)
    {
        var (reason, personCount, studyCount) = kvp.Value;
        if (existingByName.TryGetValue(kvp.Key, out var entity))
        {
            entity.OccurrenceCount = personCount;
            entity.StudyCount = studyCount;
            entity.RejectionReason = reason;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            upsertCtx.Set<RejectedInvestigatorNameEntity>().Add(new RejectedInvestigatorNameEntity
            {
                Id = Guid.NewGuid(),
                FullName = kvp.Key,
                OccurrenceCount = personCount,
                StudyCount = studyCount,
                RejectionReason = reason,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    // Remove entries that no longer fail the filter
    var toRemove = existingRejected
        .Where(e => !rejectedByFilter.ContainsKey(e.FullName))
        .ToList();
    foreach (var r in toRemove)
    {
        upsertCtx.Set<RejectedInvestigatorNameEntity>().Remove(r);
    }

    await upsertCtx.SaveChangesAsync();
}

if (leaks.Count == 0)
{
    await Console.Out.WriteLineAsync($"\n✓ PASS: No non-human entities found in investigator_persons");
    await Console.Out.WriteLineAsync($"  All {totalPersons} entries pass NameFilter.IsHumanName");
}
else
{
    var byReason = rejectedByFilter
        .GroupBy(kvp => kvp.Value.Reason ?? "Unknown")
        .Select(g => (Reason: g.Key, Count: g.Count()))
        .OrderByDescending(g => g.Count);

    await Console.Out.WriteLineAsync($"\n✗ FAIL: {leaks.Count} non-human entit{(leaks.Count == 1 ? "y" : "ies")} found:");
    await Console.Out.WriteLineAsync("  Aggregated by name (stored in rejected_investigator_names):");
    await Console.Out.WriteLineAsync($"  Total distinct rejected names: {rejectedByFilter.Count}");
    await Console.Out.WriteLineAsync("  By rejection reason:");
    foreach (var (reason, count) in byReason)
    {
        await Console.Out.WriteLineAsync($"    {reason}: {count}");
    }
    await Console.Out.WriteLineAsync();
    await Console.Out.WriteLineAsync("  Top entries:");
    foreach (var entry in rejectedByFilter.OrderByDescending(kvp => kvp.Value.StudyCount).Take(10))
    {
        await Console.Out.WriteLineAsync($"    \"{entry.Key}\" — reason={entry.Value.Reason}, {entry.Value.StudyCount} studies, {entry.Value.PersonCount} occurrences");
    }
    await Console.Out.WriteLineAsync("\nUpdate NameFilter.cs to catch these patterns, commit, then re-run.");
}

if (container != null)
{
    await container.DisposeAsync();
}

return leaks.Count > 0 ? 1 : 0;
