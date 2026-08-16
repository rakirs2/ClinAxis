using Frontend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class DataApiConfigurationTests
{
    [TestMethod]
    public void ResolveBaseUrlUsesIpv4LoopbackByDefault()
    {
        Assert.AreEqual("http://127.0.0.1:5003", DataApiConfiguration.ResolveBaseUrl(null));
    }

    [TestMethod]
    public void ResolveBaseUrlPreservesConfiguredEndpoint()
    {
        Assert.AreEqual("http://data-api:5003", DataApiConfiguration.ResolveBaseUrl("http://data-api:5003"));
    }
}
