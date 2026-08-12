using IngestionApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Services.EventQueue;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class EventProcessingServiceTests
{
    [TestMethod]
    public async Task StartAsync_RecoversLegacyDiscoveryEventsBeforePolling()
    {
        var queue = new InMemoryEventQueueService { LegacyRecoveryEventIds = [17, 23] };
        var state = new DataSourceStateService("Host=localhost;Database=not-used");
        var ingestion = new ClinicalTrialsIngestionService(
            new ClinicalTrialsGov(),
            new StudyRepository("Host=localhost;Database=not-used"));
        using var processor = new EventProcessingService(
            queue,
            state,
            ingestion,
            pollIntervalSeconds: 1);

        await processor.StartAsync(CancellationToken.None);
        try
        {
            var recovered = await queue.LegacyRecoveryCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            CollectionAssert.AreEqual(new[] { 17, 23 }, recovered);
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }
    }
}
