using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class StudyDetailPageTests
{
    [TestMethod]
    public void StudyDetailPageRendersLoadingState()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("/api/studies/*").Respond("application/json", "{}");
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.StudyDetail> cut = ctx.Render<Frontend.Pages.StudyDetail>(parameters => parameters
            .Add(p => p.nctId, "NCT00000001"));
        Assert.IsNotNull(cut.Find("h1"));
    }
}