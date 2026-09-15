using System.Net;
using System.Net.Http;
using Sinalo.Application.Synchronization;

namespace Sinalo.Tests.Unit;

public sealed class HttpSynchronizationRetryTests
{
    [Fact]
    public async Task GetStringAsync_ShouldRetryTransientServerFailure()
    {
        var handler = new SequenceHandler(
        [
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("conteúdo") }
        ]);
        using var client = new HttpClient(handler);

        var result = await HttpSynchronizationRetry.GetStringAsync(client, new Uri("https://example.test/"));

        Assert.Equal("conteúdo", result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_ShouldNotRetryPermanentClientFailure()
    {
        var handler = new SequenceHandler([new HttpResponseMessage(HttpStatusCode.NotFound)]);
        using var client = new HttpClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => HttpSynchronizationRetry.GetAsync(client, new Uri("https://example.test/")));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_ShouldRetryTemporaryNetworkFailure()
    {
        var handler = new ThrowThenSucceedHandler();
        using var client = new HttpClient(handler);

        using var response = await HttpSynchronizationRetry.GetAsync(client, new Uri("https://example.test/"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_ShouldRetryTimeoutWhenTheCallerDidNotCancel()
    {
        var handler = new TimeoutThenSucceedHandler();
        using var client = new HttpClient(handler);

        using var response = await HttpSynchronizationRetry.GetAsync(client, new Uri("https://example.test/"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class SequenceHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int _index;
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responses[Math.Min(_index++, responses.Count - 1)]);
        }
    }

    private sealed class ThrowThenSucceedHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1) throw new HttpRequestException("Sem rede");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class TimeoutThenSucceedHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1) throw new TaskCanceledException("Tempo esgotado");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
