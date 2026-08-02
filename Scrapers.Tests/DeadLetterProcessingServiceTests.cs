using IngestionApp;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class DeadLetterProcessingServiceTests
{
    [TestMethod]
    public async Task ExecuteAsync_LogsWarning_WhenDeadLetterEventsExist()
    {
        var queue = new InMemoryEventQueueService
        {
            DeadLetterEvents =
            [
                new PipelineEventEntity { Id = 1, EventType = "investigator.enrichment", Status = "dead-letter", ErrorMessage = "boom" },
                new PipelineEventEntity { Id = 2, EventType = "studies.discovered", Status = "dead-letter", ErrorMessage = "nope" }
            ]
        };
        var logger = new CapturingLogger<DeadLetterProcessingService>();
        var service = new DeadLetterProcessingService(queue, logger);

        using var serviceHandle = service;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);
        try
        {
            var countEntry = await WaitForEntryAsync(logger, e => e.Level == LogLevel.Warning && e.Message.Contains("Dead-letter queue has 2 events", StringComparison.Ordinal));
            Assert.IsNotNull(countEntry, "expected a dead-letter count warning");

            var sample = logger.Entries.Where(e => e.EventId.Id == 2).ToList();
            Assert.AreEqual(2, sample.Count);
            Assert.IsTrue(sample[0].Message.Contains("investigator.enrichment", StringComparison.Ordinal), sample[0].Message);
            Assert.IsTrue(sample[1].Message.Contains("studies.discovered", StringComparison.Ordinal), sample[1].Message);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_LogsError_WhenQueueThrows()
    {
        var queue = new InMemoryEventQueueService { ThrowOnGetDeadLetter = new InvalidOperationException("db down") };
        var logger = new CapturingLogger<DeadLetterProcessingService>();
        var service = new DeadLetterProcessingService(queue, logger);

        using var serviceHandle = service;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);
        try
        {
            var errorEntry = await WaitForEntryAsync(logger, e => e.Level == LogLevel.Error && e.Message == "DeadLetterProcessingService error");
            Assert.IsNotNull(errorEntry);
            Assert.AreEqual("db down", errorEntry.Exception?.Message);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    private static async Task<CapturingLogger<DeadLetterProcessingService>.CapturedLog?> WaitForEntryAsync(
        CapturingLogger<DeadLetterProcessingService> logger,
        Func<CapturingLogger<DeadLetterProcessingService>.CapturedLog, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var match = logger.Entries.FirstOrDefault(predicate);
            if (match != null)
            {
                return match;
            }

            await Task.Delay(10);
        }

        return null;
    }
}
