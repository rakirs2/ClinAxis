using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class MeshLocationModeTests
{
    private const string UsTreeNumber = "Z01.107.567.875";
    private const string CaliforniaTreeNumber = "Z01.107.567.875.510";

    private static readonly string[] EmptyStrings = [];
    private static readonly string[] Cities = ["San Francisco"];
    private static readonly string[] Facilities = ["Stanford"];
    private static readonly string[] Countries = ["United States"];
    private static readonly string[] States = ["CA"];

    [TestMethod]
    public void MeshLocationModePassesDescriptorNamesToDistinctLocationsCascade()
    {
        var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("/api/mesh-tree?branch=Z&depth=1")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                branch = "Z",
                nodes = new[]
                {
                    new { treeNumber = UsTreeNumber, name = "United States", studyCount = 1, hasChildren = true, children = (object?)null }
                }
            }));
        mockHttp.When($"/api/mesh-tree?branch={UsTreeNumber}&depth=1")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                branch = UsTreeNumber,
                nodes = new[]
                {
                    new { treeNumber = CaliforniaTreeNumber, name = "California", studyCount = 1, hasChildren = true, children = (object?)null }
                }
            }));
        mockHttp.When("/api/distinct-conditions").Respond("application/json", "[]");
        mockHttp.When("/api/mesh-tree*").Respond("application/json", "[]");

        var locationCalls = new List<string>();
        mockHttp.When("/api/distinct-locations*")
            .Respond(req =>
            {
                locationCalls.Add(Uri.UnescapeDataString(req.RequestUri!.Query));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new
                        {
                            countries = Countries,
                            states = States,
                            cities = Cities,
                            facilities = Facilities
                        }),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            });

        string? studiesQuery = null;
        mockHttp.When("/api/studies*")
            .Respond(req =>
            {
                studiesQuery = Uri.UnescapeDataString(req.RequestUri!.Query);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        data = EmptyStrings,
                        total = 0,
                        page = 1,
                        pageSize = 20,
                        totalPages = 0
                    }), System.Text.Encoding.UTF8, "application/json")
                };
            });

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));
        ctx.JSInterop.SetupVoid("meshTree.render", _ => true);
        ctx.JSInterop.Setup<string[]>("meshTree.getSelected", _ => true).SetResult([]);

        var cut = ctx.Render<Frontend.Pages.Search>();
        cut.WaitForState(() => cut.FindAll("#meshCountrySelect option").Count > 1, TimeSpan.FromSeconds(5));

        cut.Find("#meshCountrySelect").Change(UsTreeNumber);
        cut.WaitForState(() => cut.FindAll("#meshStateSelect option").Count > 1, TimeSpan.FromSeconds(5));

        cut.Find("#meshStateSelect").Change(CaliforniaTreeNumber);
        cut.WaitForState(() => cut.FindAll("#meshCitySelect option").Count > 1, TimeSpan.FromSeconds(5));

        cut.Find("#meshCitySelect").Change("San Francisco");
        cut.WaitForState(() => cut.FindAll("#facility_Stanford").Count > 0, TimeSpan.FromSeconds(5));

        Assert.IsTrue(locationCalls.Any(q => q.Contains("country=United States", StringComparison.Ordinal)), "States must load with the country NAME, not the Z-tree number");
        Assert.IsTrue(locationCalls.Any(q => q.Contains("state=California", StringComparison.Ordinal)), "Cities must load with the state NAME, not the Z-tree number");
        Assert.IsTrue(locationCalls.Any(q =>
            q.Contains("country=United States", StringComparison.Ordinal) &&
            q.Contains("state=California", StringComparison.Ordinal) &&
            q.Contains("city=San Francisco", StringComparison.Ordinal)), "Facilities must load with country/state NAMES + city");

        Assert.IsNotNull(studiesQuery);
        StringAssert.Contains(studiesQuery!, $"locationMeshTree={UsTreeNumber},{CaliforniaTreeNumber}", StringComparison.Ordinal);

        ctx.Dispose();
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }
        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
