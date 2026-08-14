using System.Net;
using Frontend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class ProxyConfigurationTests
{
    [TestMethod]
    public void ConfigureForwardedHeadersTrustsLocalCaddyAndOriginalScheme()
    {
        var options = new ForwardedHeadersOptions();

        ProxyConfiguration.ConfigureForwardedHeaders(options);

        Assert.AreEqual(ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.IsTrue(options.KnownProxies.Contains(IPAddress.Loopback));
    }
}
