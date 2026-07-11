using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class HealthCheckTests
{
    [TestMethod]
    public async Task FrontendHealthCheckEndpointReturnsHealthyStatus()
    {
        // Arrange
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri("http://localhost/health"));

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("Healthy", content);
    }

    [TestMethod]
    public async Task FrontendHealthCheckEndpointHasCorrectContentType()
    {
        // Arrange
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri("http://localhost/health"));

        // Assert
        Assert.IsTrue(response.Content.Headers.ContentType?.MediaType?.Contains("text/plain", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [TestMethod]
    public async Task HealthCheckEndpointIsAccessibleBeforeAppFullyInitializes()
    {
        // Arrange - Simulate health check being called very soon after app starts
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        // Act - Make multiple rapid requests to simulate deployment health check retries
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.GetAsync(new Uri("http://localhost/health")))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed with 200
        foreach (var response in responses)
        {
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        }
    }
}
