using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Services.EventQueue;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class PerformanceTests
{
    private static SnapshotDb _snapshot = null!;
    private static StudyRepository _repo = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        _snapshot = new SnapshotDb();
        _repo = new StudyRepository(_snapshot.ConnectionString);
        await SeedSearchDataAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await _snapshot.DisposeAsync();
    }

    // --- Index Schema Guard ---

    [TestMethod, TestCategory("Integration")]
    public async Task StudiesTable_HasIndexOnOverallStatusAndStartDate()
    {
        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_snapshot.ConnectionString)
                .Options);

        var indexes = await ctx.Database.SqlQuery<string>($@"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'studies'
            AND indexname LIKE 'IX_studies_%'
        ").ToListAsync();

        var match = indexes.FirstOrDefault(i =>
            i.Contains("overall_status", StringComparison.Ordinal) && i.Contains("start_date", StringComparison.Ordinal));
        Assert.IsNotNull(match,
            "Missing index on studies(overall_status, start_date). " +
            $"Found indexes: {string.Join(", ", indexes)}");
    }

    [TestMethod, TestCategory("Integration")]
    public async Task StudiesTable_HasIndexOnEnrollmentCount()
    {
        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_snapshot.ConnectionString)
                .Options);

        var indexes = await ctx.Database.SqlQuery<string>($@"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'studies'
            AND indexname LIKE 'IX_studies_%'
        ").ToListAsync();

        var match = indexes.FirstOrDefault(i => i.Contains("enrollment_count", StringComparison.Ordinal));
        Assert.IsNotNull(match,
            "Missing index on studies(enrollment_count). " +
            $"Found indexes: {string.Join(", ", indexes)}");
    }

    [TestMethod, TestCategory("Integration")]
    public async Task InvestigatorPersonsTable_HasIndexOnIsHumanAndNpiEnrichmentResult()
    {
        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_snapshot.ConnectionString)
                .Options);

        var indexes = await ctx.Database.SqlQuery<string>($@"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'investigator_persons'
            AND indexname LIKE 'IX_investigator_persons_%'
        ").ToListAsync();

        var match = indexes.FirstOrDefault(i =>
            i.Contains("is_human", StringComparison.Ordinal) && i.Contains("npi_enrichment_result", StringComparison.Ordinal));
        Assert.IsNotNull(match,
            "Missing index on investigator_persons(is_human, npi_enrichment_result). " +
            $"Found indexes: {string.Join(", ", indexes)}");
    }

    // --- Latency Benchmarks ---

    private const int SeedCount = 10_000;

    private static async Task SeedSearchDataAsync()
    {
        var statuses = new[] { "RECRUITING", "ACTIVE", "COMPLETED", "TERMINATED", "WITHDRAWN" };
        var batch = new List<ClinicalTrialRecord>();

        for (int i = 0; i < SeedCount; i++)
        {
            var nctId = $"NCTPERF{i:D8}";
            var status = statuses[i % statuses.Length];
            var startDate = new DateOnly(2020, 1, 1).AddDays(i % 2000);
            var enrollment = (i % 999) * 10 + 10;

            batch.Add(new ClinicalTrialRecord
            {
                NctId = nctId,
                BriefTitle = $"Performance Test Study {i} - {status}",
                OverallStatus = status,
                StartDate = startDate,
                EnrollmentCount = enrollment
            });
        }

        await _repo.UpdateStudiesWithClinicalTrialsAsync(batch);
    }

    [TestMethod, TestCategory("Integration")]
    public async Task SearchStudies_ByStatus_CompletesUnderThreshold()
    {
        var sw = Stopwatch.StartNew();

        var results = await _repo.SearchStudiesAsync(new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            Page = 1,
            PageSize = 20
        });

        sw.Stop();

        Assert.IsTrue(results.Count > 0, "Expected at least one matching study");
        Assert.IsTrue(sw.ElapsedMilliseconds < 500,
            $"Search by status took {sw.ElapsedMilliseconds}ms (threshold: 500ms)");
    }

    [TestMethod, TestCategory("Integration")]
    public async Task SearchStudies_ByStatusAndDateRange_CompletesUnderThreshold()
    {
        var sw = Stopwatch.StartNew();

        var results = await _repo.SearchStudiesAsync(new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING", "ACTIVE" },
            StartDateFrom = new DateTime(2023, 1, 1),
            StartDateTo = new DateTime(2024, 12, 31),
            Page = 1,
            PageSize = 20
        });

        sw.Stop();

        Assert.IsTrue(sw.ElapsedMilliseconds < 500,
            $"Search by status + date range took {sw.ElapsedMilliseconds}ms (threshold: 500ms)");
    }

    [TestMethod, TestCategory("Integration")]
    public async Task SearchStudies_WithEnrollmentFilter_CompletesUnderThreshold()
    {
        var sw = Stopwatch.StartNew();

        var results = await _repo.SearchStudiesAsync(new StudySearchCriteria
        {
            EnrollmentMin = 1000,
            EnrollmentMax = 5000,
            Page = 1,
            PageSize = 20
        });

        sw.Stop();

        Assert.IsTrue(sw.ElapsedMilliseconds < 500,
            $"Search by enrollment range took {sw.ElapsedMilliseconds}ms (threshold: 500ms)");
    }

    [TestMethod, TestCategory("Integration")]
    public async Task CountStudies_ByStatus_CompletesUnderThreshold()
    {
        var sw = Stopwatch.StartNew();

        var count = await _repo.CountStudiesFilteredAsync(new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING", "ACTIVE", "COMPLETED" }
        });

        sw.Stop();

        Assert.IsTrue(count > 0, "Expected non-zero count");
        Assert.IsTrue(sw.ElapsedMilliseconds < 500,
            $"Count by status took {sw.ElapsedMilliseconds}ms (threshold: 500ms)");
    }

    // --- Lock Contention ---

    private const int BatchSize = 100;

    private static async Task SeedBaselineStudiesAsync()
    {
        var batch = new List<ClinicalTrialRecord>();
        for (int i = 0; i < 500; i++)
        {
            batch.Add(new ClinicalTrialRecord
            {
                NctId = $"NCTLOC{i:D8}",
                BriefTitle = $"Lock Contention Baseline Study {i}",
                OverallStatus = "RECRUITING",
                StartDate = new DateOnly(2023, 1, 1).AddDays(i)
            });
        }
        await _repo.UpdateStudiesWithClinicalTrialsAsync(batch);
    }

    [TestMethod, TestCategory("Integration")]
    public async Task ConcurrentReadDuringWrite_CompletesUnderThreshold()
    {
        await SeedBaselineStudiesAsync();

        var writeRecords = new List<ClinicalTrialRecord>();
        for (int i = 0; i < BatchSize; i++)
        {
            writeRecords.Add(new ClinicalTrialRecord
            {
                NctId = $"NCTCONC{i:D8}",
                BriefTitle = $"Concurrent Write Study {i}",
                OverallStatus = "ACTIVE",
                StartDate = new DateOnly(2024, 1, 1).AddDays(i)
            });
        }

        var writeTask = Task.Run(async () =>
        {
            var writeRepo = new StudyRepository(_snapshot.ConnectionString);
            await writeRepo.UpdateStudiesWithClinicalTrialsAsync(writeRecords);
        });

        await Task.Delay(200);

        var readSw = Stopwatch.StartNew();

        var readRepo = new StudyRepository(_snapshot.ConnectionString);
        var results = await readRepo.SearchStudiesAsync(new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            Page = 1,
            PageSize = 20
        });

        readSw.Stop();

        await writeTask;

        Assert.IsTrue(results.Count > 0, "Reader should return results during concurrent write");
        Assert.IsTrue(readSw.ElapsedMilliseconds < 1000,
            $"Concurrent read took {readSw.ElapsedMilliseconds}ms (threshold: 1000ms)");
    }

    // --- Event Queue Concurrency ---

    [TestMethod, TestCategory("Integration")]
    public async Task TwoConcurrentClaims_ReturnDistinctEvents()
    {
        var service1 = new EventQueueService(_snapshot.ConnectionString);
        var service2 = new EventQueueService(_snapshot.ConnectionString);

        await service1.EnqueueAsync("TestConcurrentClaim", "payload-1");
        await service2.EnqueueAsync("TestConcurrentClaim", "payload-2");

        var claimTask1 = Task.Run(async () =>
            await service1.ClaimNextPendingEventAsync("claimant-1", ["TestConcurrentClaim"]));

        var claimTask2 = Task.Run(async () =>
            await service2.ClaimNextPendingEventAsync("claimant-2", ["TestConcurrentClaim"]));

        var results = await Task.WhenAll(claimTask1, claimTask2);

        var claimed = results.Where(r => r != null).Select(r => r!.Id).ToList();

        Assert.AreEqual(2, claimed.Count,
            $"Expected 2 distinct claims, got {claimed.Count}. " +
            "Race condition in ClaimNextPendingEventAsync may be claiming the same event twice.");
        Assert.AreNotEqual(claimed[0], claimed[1],
            "Two concurrent claims returned the same event ID — race condition detected.");
    }

    [TestMethod, TestCategory("Integration")]
    public async Task ConcurrentClaims_WithSingleEvent_OnlyOneClaimSucceeds()
    {
        var service1 = new EventQueueService(_snapshot.ConnectionString);
        var service2 = new EventQueueService(_snapshot.ConnectionString);

        await service1.EnqueueAsync("TestSingleClaim", "single-payload");

        var claimTask1 = Task.Run(async () =>
            await service1.ClaimNextPendingEventAsync("claimant-1", ["TestSingleClaim"]));

        var claimTask2 = Task.Run(async () =>
            await service2.ClaimNextPendingEventAsync("claimant-2", ["TestSingleClaim"]));

        var results = await Task.WhenAll(claimTask1, claimTask2);

        var claimed = results.Where(r => r != null).ToList();

        Assert.AreEqual(1, claimed.Count,
            $"Expected exactly 1 claim on single event, got {claimed.Count}. " +
            "Race condition allows duplicate claims.");
    }
}
