using System.Net;
using System.Text;

namespace LinerNotes.Infrastructure.Tests.Fakes;

/// <summary>
/// Configurable HttpMessageHandler fake for deterministic HTTP testing without external network calls.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerFunc;
    private readonly List<HttpRequestMessage> _recordedRequests = new();

    public IReadOnlyList<HttpRequestMessage> RecordedRequests => _recordedRequests;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
    {
        _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
    }

    public static MockHttpMessageHandler WithJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });
    }

    public static MockHttpMessageHandler WithStatusCode(HttpStatusCode statusCode)
    {
        return new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(statusCode)));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _recordedRequests.Add(request);
        return await _handlerFunc(request).ConfigureAwait(false);
    }
}
