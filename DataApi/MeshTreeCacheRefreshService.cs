using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataApi;

internal sealed partial class MeshTreeCountRefreshService : BackgroundService
{
    private readonly MeshTreeStore _store;
    private readonly ILogger<MeshTreeCountRefreshService> _logger;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    public MeshTreeCountRefreshService(
        MeshTreeStore store,
        ILogger<MeshTreeCountRefreshService> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _store.RefreshCountsAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                CountRefreshFailed.Log(_logger, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                CountRefreshFailed.Log(_logger, ex.Message);
            }

            await Task.Delay(RefreshInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static partial class CountRefreshFailed
    {
        [LoggerMessage(LogLevel.Warning, "MeSH study count refresh failed: {Message}")]
        public static partial void Log(ILogger logger, string message);
    }
}
