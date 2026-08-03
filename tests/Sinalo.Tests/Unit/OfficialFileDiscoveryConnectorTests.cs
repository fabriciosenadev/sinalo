using System.Net;
using System.Net.Http;
using System.Text;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class OfficialFileDiscoveryConnectorTests
{
    [Fact]
    public async Task DiscoverAsync_ShouldCreatePendingItemsOnlyForHttpsDirectVideoLinks()
    {
        const string html = """
            <a href="https://downloads.example/video.mp4">Vídeo</a>
            <a href="/relative/second.webm">Segundo</a>
            <a href="https://youtube.com/watch?v=abc">YouTube</a>
            <a href="http://example.test/insecure.mov">Inseguro</a>
            <a href="https://downloads.example/video.mp4">Duplicado</a>
            """;
        var client = new HttpClient(new StubHandler(html));
        var connector = new OfficialFileDiscoveryConnector(ContentSource.ProvaiEVede, client);

        var items = await connector.DiscoverAsync(new SourceConfiguration(ContentSource.ProvaiEVede, "Provai e Vede", "https://official.example/page", AvailabilityPolicy.QuarterlyFull));

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(SyncState.Pending, item.SyncState));
        Assert.Contains(items, item => item.Assets.Single().DownloadUri.AbsoluteUri == "https://downloads.example/video.mp4");
        Assert.Contains(items, item => item.Assets.Single().DownloadUri.AbsoluteUri == "https://official.example/relative/second.webm");
    }

    [Fact]
    public async Task DiscoverAsync_ShouldKeepTheConfiguredSourceOnEveryItem()
    {
        var connector = new OfficialFileDiscoveryConnector(ContentSource.Missions, new HttpClient(new StubHandler("<a href='https://files.example/mission.m4v'>Arquivo</a>")));

        var item = Assert.Single(await connector.DiscoverAsync(new SourceConfiguration(ContentSource.Missions, "Informativo", "https://official.example/", AvailabilityPolicy.MonthlyFull)));

        Assert.Equal(ContentSource.Missions, item.Source);
        Assert.Equal("mission.m4v", item.Title);
        Assert.Equal(item.PageUri, item.Assets.Single().DownloadUri);
    }

    private sealed class StubHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") });
    }
}
