using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class InvestigatorFinderPageTests
{
    private static readonly object Investigator1 = new
    {
        uuid = "11111111-1111-1111-1111-111111111111",
        name = "Alice Expert",
        primaryAffiliation = "Harvard",
        score = 0.90,
        modelScore = 0.95,
        factors = new { relevance = 0.9, experience = 0.9, publication = 0.5, network = 0.5 },
        details = new { studyCount = 10, completedStudies = 9, enrollmentTotal = 1500, hIndex = 20, paperCount = 40 }
    };

    private static readonly object Investigator2 = new
    {
        uuid = "22222222-2222-2222-2222-222222222222",
        name = "Bob Novice",
        primaryAffiliation = "Stanford",
        score = 0.75,
        modelScore = 0.60,
        factors = new { relevance = 0.5, experience = 0.6, publication = 0.4, network = 0.4 },
        details = new { studyCount = 2, completedStudies = 1, enrollmentTotal = 120, hIndex = 5, paperCount = 8 }
    };

    private static readonly object FinderResponse = new
    {
        investigators = new[] { Investigator1, Investigator2 },
        totalCandidates = 2
    };

    private static (BunitContext Ctx, MockHttpMessageHandler Mock, IRenderedComponent<Frontend.Pages.InvestigatorFinder> Cut) SetupTest(
        object? finderResponse = null)
    {
        var ctx = new BunitContext();
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("/api/mesh-tree/search*")
            .Respond("application/json", JsonSerializer.Serialize(new[]
            {
                new { descriptorId = 1001, name = "Neuropathic Pain", treeNumber = "C10.500", studyCount = 5 }
            }));
        mockHttp.When("/api/investigator-finder")
            .Respond("application/json", JsonSerializer.Serialize(finderResponse ?? FinderResponse));
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        var cut = ctx.Render<Frontend.Pages.InvestigatorFinder>();
        return (ctx, mockHttp, cut);
    }

    private static async Task<IRenderedComponent<Frontend.Pages.InvestigatorFinder>> FindInvestigators(
        IRenderedComponent<Frontend.Pages.InvestigatorFinder> cut)
    {
        var input = cut.Find("input[placeholder*='conditions']");
        await input.InputAsync("neuro");
        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(2));
        await cut.FindAll("li.list-group-item")[0].ClickAsync();
        cut.WaitForState(() => cut.FindAll("span.badge.bg-info").Count > 0, TimeSpan.FromSeconds(1));
        await cut.Find("button:contains('Find Investigators')").ClickAsync();
        cut.WaitForState(() => cut.FindAll("div.col-md-6 div.card").Count > 0, TimeSpan.FromSeconds(2));
        return cut;
    }

    [TestMethod]
    public async Task FinderPageShowsRuleScoreAndModelScoreBadges()
    {
        var (ctx, _, cut) = SetupTest();
        await FindInvestigators(cut);

        var cards = cut.FindAll("div.col-md-6 div.card");
        Assert.AreEqual(2, cards.Count);
        Assert.IsTrue(cut.FindAll("span.badge.bg-primary").Count == 2, "expected two rule-score badges");
        Assert.IsTrue(cut.FindAll("span.badge.bg-success").Count == 2, "expected two model-score badges");

        var firstCard = cards[0].TextContent;
        Assert.IsTrue(firstCard.Contains("0.90", StringComparison.Ordinal), "rule score 0.90 not visible");
        Assert.IsTrue(firstCard.Contains("0.95", StringComparison.Ordinal), "model score 0.95 not visible");
        await ctx.DisposeAsync();
    }

    [TestMethod]
    public async Task FinderPageSortByModelScoreReordersResults()
    {
        var (ctx, _, cut) = SetupTest();
        await FindInvestigators(cut);

        var ruleFirst = cut.FindAll("div.col-md-6 div.card")[0].TextContent;
        Assert.IsTrue(ruleFirst.Contains("Alice Expert", StringComparison.Ordinal),
            "default sort should put higher rule score first");

        await cut.Find("button:contains('Sort by Model Score')").ClickAsync();
        cut.WaitForState(() =>
        {
            var first = cut.FindAll("div.col-md-6 div.card")[0].TextContent;
            return first.Contains("Alice Expert", StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(1));

        var modelFirst = cut.FindAll("div.col-md-6 div.card")[0].TextContent;
        Assert.IsTrue(modelFirst.Contains("Alice Expert", StringComparison.Ordinal),
            "Alice has the higher model score (0.95) and should be first after re-sort");
        await ctx.DisposeAsync();
    }

    [TestMethod]
    public async Task FinderPageShowsPlaceholderWhenModelScoreMissing()
    {
        var (ctx, _, cut) = SetupTest(new
        {
            investigators = new[]
            {
                new
                {
                    uuid = "11111111-1111-1111-1111-111111111111",
                    name = "Alice Expert",
                    primaryAffiliation = "Harvard",
                    score = 0.90,
                    modelScore = (double?)null,
                    factors = new { relevance = 0.9, experience = 0.9, publication = 0.5, network = 0.5 },
                    details = new { studyCount = 10, completedStudies = 9, enrollmentTotal = 1500, hIndex = 20, paperCount = 40 }
                }
            },
            totalCandidates = 1
        });
        await FindInvestigators(cut);

        Assert.IsTrue(cut.FindAll("span.badge.bg-secondary").Count == 1,
            "expected placeholder badge when modelScore is null");
        await ctx.DisposeAsync();
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
