using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace Scrapers.Tests.Helpers;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<HttpResponseMessage> _responses = new();
    private readonly List<Uri> _requests = new();

    public IReadOnlyList<Uri> Requests => _requests;

    public void EnqueueJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request.RequestUri ?? new Uri("http://localhost"));

        if (!_responses.TryDequeue(out var response))
        {
            throw new InvalidOperationException("No fake responses remaining for ClinicalTrials.gov request.");
        }

        return Task.FromResult(response);
    }
}
