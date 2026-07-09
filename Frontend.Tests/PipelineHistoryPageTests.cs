using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Bunit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class PipelineHistoryPageTests
{
    [TestMethod]
    public void PipelineHistoryPageRendersTitle()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("/api/pipeline-runs").Respond("application/json", JsonSerializer.Serialize(new { data = Array.Empty<object>() }));
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.PipelineHistory> cut = ctx.Render<Frontend.Pages.PipelineHistory>();
        Assert.IsNotNull(cut.Find("h1"));
        Assert.AreEqual("Pipeline History", cut.Find("h1").TextContent);
    }
}