using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataApi;

internal sealed partial class MeshTreeCacheRefreshService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MeshTreeCacheRefreshService> _logger;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly Uri MeshTreeUri = new("http://localhost:5003/api/mesh-tree?branch=C&minStudyCount=1&depth=5");

    public MeshTreeCacheRefreshService(
        IHttpClientFactory httpClientFactory,
        ILogger<MeshTreeCacheRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var client = _httpClientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync(MeshTreeUri, stoppingToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    MeshTreeRefreshFailed.Log(_logger, (int)response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException ex)
            {
                MeshTreeRefreshError.Log(_logger, ex.Message);
            }

            await Task.Delay(RefreshInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static partial class MeshTreeRefreshFailed
    {
        [LoggerMessage(LogLevel.Warning, "MeSH tree cache refresh returned {StatusCode}")]
        public static partial void Log(ILogger logger, int statusCode);
    }

    private static partial class MeshTreeRefreshError
    {
        [LoggerMessage(LogLevel.Warning, "MeSH tree cache refresh failed: {Message}")]
        public static partial void Log(ILogger logger, string message);
    }
}
