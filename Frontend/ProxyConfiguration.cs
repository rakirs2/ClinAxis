using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Frontend;

internal static class ProxyConfiguration
{
    internal static void ConfigureForwardedHeaders(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Add(IPAddress.Loopback);
    }
}
