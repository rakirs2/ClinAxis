using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class HealthCheckTests
{
    [TestMethod]
    public async Task HealthEndpointReturns200WithStatusAndVersion()
    {
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync(new Uri("http://localhost/health"));

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("Frontend", doc.RootElement.GetProperty("application").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("version", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("informationalVersion", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("framework", out _));
    }

    [TestMethod]
    public async Task HealthEndpointIsAccessibleBeforeAppFullyInitializes()
    {
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.GetAsync(new Uri("http://localhost/health")))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        }
    }
}
