using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class StatusPageTests
{
    [TestMethod]
    public void StatusPageRendersTitle()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://localhost:5003/api/stats").Respond("application/json", JsonSerializer.Serialize(new
        {
            totalStudies = 100
        }));
        mockHttp.When("http://localhost:5003/api/pipeline-runs?page=1&pageSize=1").Respond("application/json", JsonSerializer.Serialize(new
        {
            data = Array.Empty<object>()
        }));
        mockHttp.When("http://localhost:5003/api/aggregations").Respond("application/json", JsonSerializer.Serialize(new
        {
            piAggregationCount = 0,
            categoryAggregationCount = 0,
            categoryAggregationsByType = new object()
        }));
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();
        Assert.IsNotNull(cut.Find("h1"));
        Assert.AreEqual("System Status", cut.Find("h1").TextContent);
    }
}
