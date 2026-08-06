using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class RecommendInvestigatorsPageTests
{
    private static readonly string[] Regions = ["United States"];

    private static (BunitContext Ctx, MockHttpMessageHandler Mock) Setup(
        object? recommendResponse = null, Action<HttpRequestMessage>? captureRequest = null)
    {
        var ctx = new BunitContext();
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("/api/mesh-tree/search*").Respond("application/json", JsonSerializer.Serialize(new[]
        {
            new { descriptorId = 1, name = "Cardiovascular", treeNumber = "C14", studyCount = 5 }
        }));

        mockHttp.When("/api/recommend/investigators")
            .Respond(req =>
            {
                captureRequest?.Invoke(req);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(recommendResponse ?? DefaultResponse()),
                        Encoding.UTF8,
                        "application/json")
                };
            });

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        return (ctx, mockHttp);
    }

    private static object DefaultResponse() => new
    {
        totalCandidates = 1,
        investigators = new[]
        {
            new
            {
                uuid = Guid.NewGuid(),
                name = "Dr. David Williams, MD",
                primaryAffiliation = "Stanford Medical Center",
                score = 0.85,
                factors = new
                {
                    experience = 0.5,
                    completion = 1.0,
                    conditionFit = 1.0,
                    velocity = 0.4,
                    publication = 0.6,
                    geographic = (double?)1.0
                },
                details = new
                {
                    studyCount = 12,
                    completionRate = 0.9,
                    enrollmentVelocity = 8.5,
                    hIndex = 24,
                    paperCount = 61,
                    regions = Regions
                }
            }
        }
    };

    [TestMethod]
    public void PageRendersRequestForm()
    {
        var (ctx, _) = Setup();
        var cut = ctx.Render<Frontend.Pages.RecommendInvestigators>();

        Assert.IsNotNull(cut.Find("input[placeholder='e.g. pediatric, elderly']"));
        Assert.IsNotNull(cut.Find("input[placeholder='e.g. United States']"));
        Assert.IsNotNull(cut.Find("input[placeholder='e.g. 300']"));
        Assert.IsNotNull(cut.Find("button:contains('Recommend Investigators')"));
        ctx.Dispose();
    }

    [TestMethod]
    public void SubmitWithoutSelectionsShowsValidationError()
    {
        var (ctx, _) = Setup();
        var cut = ctx.Render<Frontend.Pages.RecommendInvestigators>();

        cut.Find("button:contains('Recommend Investigators')").Click();

        Assert.IsNotNull(cut.Find("div.alert-danger"));
        Assert.IsTrue(cut.Find("div.alert-danger").TextContent.Contains("therapy or condition", StringComparison.Ordinal));
        ctx.Dispose();
    }

    [TestMethod]
    public void SubmitWithSelectionPostsRequestAndRendersRankedResults()
    {
        HttpRequestMessage? captured = null;
        var (ctx, _) = Setup(captureRequest: req => captured = req);

        var cut = ctx.Render<Frontend.Pages.RecommendInvestigators>();

        var therapyInput = cut.Find("input[placeholder='Search therapy, drug class, intervention...']");
        therapyInput.Input("cardio");
        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(5));
        cut.FindAll("li.list-group-item")[0].Click();

        cut.Find("button:contains('Recommend Investigators')").Click();
        cut.WaitForState(() => cut.FindAll("a[href*='/investigator/']").Count > 0, TimeSpan.FromSeconds(5));

        Assert.IsNotNull(captured, "Request must be posted to /api/recommend/investigators");
        using var doc = JsonDocument.Parse(ReadBody(captured!));
        Assert.IsTrue(doc.RootElement.TryGetProperty("topN", out _), "topN must be sent");

        var link = cut.Find("a[href*='/investigator/']");
        Assert.IsNotNull(link);
        Assert.IsTrue(cut.FindAll("div.progress-bar").Count >= 6, "All six factor bars must render");
        ctx.Dispose();
    }

    private static string ReadBody(HttpRequestMessage request)
    {
        return request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
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
