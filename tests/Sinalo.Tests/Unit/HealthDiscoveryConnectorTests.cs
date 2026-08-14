using System.Net;
using System.Net.Http;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class HealthDiscoveryConnectorTests
{
    [Fact]
    public async Task DiscoverAsync_ShouldFindTheCurrentQuarterAndParseDatedOfficialMp4Files()
    {
        const string home = "<a href='https://downloads.example/pt/saude/video/momento-vida-e-saude-3trim-2026/' title='Momento Vida e Saúde - 3TRIM 2026'>Coleção</a>";
        const string quarter = """
            <tr><td><span title='6. 08/08 - Ansiedade e Tristeza'>MP4 6. 08/08 - Ansiedade e Tristeza</span></td><td>343MB</td><td><a href='https://files.example/116_MVS_08_08_26_ANSIEDADE.mp4'>Baixar</a></td></tr>
            <tr><td><span title='7. 15/08 - Alimentos contra rugas e manchas'>MP4 7. 15/08 - Alimentos contra rugas e manchas</span></td><td>398MB</td><td><a href='https://files.example/117_MVS_15_08_26_ALIMENTOS.mp4'>Baixar</a></td></tr>
            """;
        var connector = new HealthDiscoveryConnector(new HttpClient(new PagesHandler(home, quarter)), () => new DateOnly(2026, 8, 6));

        var items = await connector.DiscoverAsync(Configuration());

        Assert.Equal(2, items.Count);
        var first = items[0];
        Assert.Equal(new DateOnly(2026, 8, 8), first.ScheduledDate);
        Assert.Equal("Ansiedade e Tristeza", first.Title);
        Assert.Equal(343L * 1024 * 1024, first.Assets.Single().ExpectedSizeBytes);
        Assert.Equal("https://files.example/116_MVS_08_08_26_ANSIEDADE.mp4", first.Assets.Single().DownloadUri.AbsoluteUri);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldAcceptTheCurrentQuarterPageConfiguredDirectly()
    {
        const string quarter = "<tr><td><span title='6. 08/08 - Ansiedade'>MP4</span></td><td>343MB</td><td><a href='https://files.example/health.mp4'>Baixar</a></td></tr>";
        var connector = new HealthDiscoveryConnector(new HttpClient(new PagesHandler("", quarter)), () => new DateOnly(2026, 8, 6));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration("https://downloads.example/pt/saude/video/momento-vida-e-saude-3trim-2026/")));

        Assert.Equal("Ansiedade", item.Title);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldUseThePredictableQuarterPageWhenTheSourceDoesNotLinkToIt()
    {
        const string quarter = "<tr><td><span title='6. 08/08 - Ansiedade'>MP4</span></td><td>343MB</td><td><a href='https://files.example/health.mp4'>Baixar</a></td></tr>";
        var connector = new HealthDiscoveryConnector(new HttpClient(new PagesHandler("<a href='/pt/saude/video/momento-vida-e-saude/'>Coleção geral</a>", quarter)), () => new DateOnly(2026, 8, 6));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal(new Uri("https://downloads.example/pt/saude/video/momento-vida-e-saude-3trim-2026/"), item.PageUri);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldRejectAnotherSource()
    {
        var connector = new HealthDiscoveryConnector(new HttpClient(new PagesHandler("", "")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.DiscoverAsync(Configuration() with { Source = ContentSource.Missions }));
    }

    private static SourceConfiguration Configuration(string url = "https://downloads.example/pt/") => new(ContentSource.Health, "Minuto de Saúde", url, AvailabilityPolicy.QuarterlyFull);
    private sealed class PagesHandler(string home, string quarter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.AbsolutePath.Contains("momento-vida-e-saude", StringComparison.OrdinalIgnoreCase) ? quarter : home) });
    }
}
