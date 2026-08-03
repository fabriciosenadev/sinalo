using System.Net;
using System.Net.Http;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class ProvaiEVedeDiscoveryConnectorTests
{
    [Fact]
    public async Task DiscoverAsync_ShouldSelectTheCurrentQuarterPageFromTheConfiguredSource()
    {
        const string html = """
            <a href="https://downloads.adventistas.org/pt/mordomia-crista/video/provai-e-vede-2026-2o-trimestre/">Segundo</a>
            <a href="/pt/mordomia-crista/video/provai-e-vede-2026-3o-trimestre/">Terceiro</a>
            """;
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler(html)), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal("provai-e-vede-2026-t3", item.Id);
        Assert.Equal("Provai e Vede 2026 - 3º Trimestre", item.Title);
        Assert.Equal(new Uri("https://www.adventistas.org/pt/mordomia-crista/video/provai-e-vede-2026-3o-trimestre/"), item.PageUri);
        Assert.Empty(item.Assets);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldReturnNoItemWhenTheCurrentQuarterIsNotPublished()
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler("<a href='/provai-e-vede-2026-2o-trimestre/'>Segundo</a>")), () => new DateOnly(2026, 8, 2));

        var items = await connector.DiscoverAsync(Configuration());

        Assert.Empty(items);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldIgnoreAnInsecureQuarterLink()
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler("<a href='http://files.example/provai-e-vede-2026-3o-trimestre/'>Terceiro</a>")), () => new DateOnly(2026, 8, 2));

        var items = await connector.DiscoverAsync(Configuration());

        Assert.Empty(items);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldRejectAnotherSource()
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler("")));
        var configuration = Configuration() with { Source = ContentSource.Missions };

        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.DiscoverAsync(configuration));
    }

    [Fact]
    public async Task DiscoverAsync_ShouldResolveVideosFromTheIdentifiedQuarterPage()
    {
        const string source = "<a href='https://downloads.example/provai-e-vede-2026-3o-trimestre/'>Terceiro</a>";
        const string quarter = "<table><tr><td>MP4 7. (08/08) Cada Centavo Conta</td><td>140MB</td><td><a href='https://files.example/08-08-26_cada-centavo.mp4'>Baixar</a></td></tr></table>";
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new PagesHandler(source, quarter)), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal(new DateOnly(2026, 8, 8), item.ScheduledDate);
        Assert.Equal("Cada Centavo Conta", item.Title);
        Assert.Equal("https://files.example/08-08-26_cada-centavo.mp4", item.Assets.Single().DownloadUri.AbsoluteUri);
    }

    private static SourceConfiguration Configuration() => new(ContentSource.ProvaiEVede, "Provai e Vede", "https://www.adventistas.org/pt/mordomiacrista/projeto/provai-e-vede/", AvailabilityPolicy.QuarterlyFull);

    private sealed class HtmlHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }

    private sealed class PagesHandler(string source, string quarter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.Host == "downloads.example" ? quarter : source) });
    }
}
