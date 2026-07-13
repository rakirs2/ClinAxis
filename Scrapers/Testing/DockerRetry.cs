using Docker.DotNet;
using DotNet.Testcontainers.Containers;

namespace Scrapers.Testing;

public static class DockerRetry
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    public static async Task StartWithRetryAsync(IContainer container, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        var exceptions = new List<Exception>();
        foreach (var delay in Backoff)
        {
            try
            {
                await container.StartAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                exceptions.Add(ex);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        throw new AggregateException(
            $"Docker container failed to start after {Backoff.Length} attempts", exceptions);
    }

    public static void StartWithRetry(IContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        var exceptions = new List<Exception>();
        foreach (var delay in Backoff)
        {
            try
            {
                container.StartAsync().GetAwaiter().GetResult();
                return;
            }
            catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                exceptions.Add(ex);
                Thread.Sleep(delay);
            }
        }

        throw new AggregateException(
            $"Docker container failed to start after {Backoff.Length} attempts", exceptions);
    }
}
