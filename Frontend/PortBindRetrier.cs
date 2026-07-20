using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;

namespace Frontend;

internal static class PortBindRetrier
{
    internal static void Run(Func<WebApplication> factory, int maxRetries = 10, TimeSpan? initialDelay = null)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var app = factory();
                app.Run();
                return;
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse && attempt < maxRetries)
            {
                Console.Error.WriteLine($"Port 5001 is in use (attempt {attempt}/{maxRetries}). Retrying in {delay.TotalSeconds:F1}s...");
                Thread.Sleep(delay);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
            }
        }
    }
}
