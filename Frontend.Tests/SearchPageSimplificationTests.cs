using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class SearchPageSimplificationTests
{
    [TestMethod]
    public void SearchPageDoesNotRenderLocationOrFacilityFilters()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        var distinctLocationRequests = 0;

        mockHttp.When("/api/distinct-conditions")
            .Respond("application/json", "[]");
        mockHttp.When("/api/mesh-tree*")
            .Respond("application/json", JsonSerializer.Serialize(new { nodes = Array.Empty<object>() }));
        mockHttp.When("/api/distinct-locations*")
            .Respond(_ =>
            {
                distinctLocationRequests++;
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        var cut = ctx.Render<Frontend.Pages.Search>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsFalse(cut.Markup.Contains("Location Mode", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Raw Locations", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Country (MeSH)", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Facility", StringComparison.Ordinal));
        }, TimeSpan.FromSeconds(5));

        Assert.AreEqual(0, distinctLocationRequests);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }
}
