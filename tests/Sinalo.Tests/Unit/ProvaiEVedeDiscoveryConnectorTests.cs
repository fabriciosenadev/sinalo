using System.Net;
using System.Net.Http;
using Sinalo.Application.Configuration;
using Sinalo.Application.Synchronization;
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
    public async Task DiscoverAsync_ShouldDiagnoseWhenTheCurrentQuarterIsNotPublished()
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler("<a href='/provai-e-vede-2026-2o-trimestre/'>Segundo</a>")), () => new DateOnly(2026, 8, 2));

        await Assert.ThrowsAsync<SiteStructureChangedException>(() => connector.DiscoverAsync(Configuration()));
    }

    [Fact]
    public async Task DiscoverAsync_ShouldIgnoreAnInsecureQuarterLink()
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler("<a href='http://files.example/provai-e-vede-2026-3o-trimestre/'>Terceiro</a>")), () => new DateOnly(2026, 8, 2));

        await Assert.ThrowsAsync<SiteStructureChangedException>(() => connector.DiscoverAsync(Configuration()));
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

    [Theory]
    [InlineData("", "provai-e-vede-2026-10-03", "Mesmo nome", "video.mp4")]
    [InlineData("-libras", "provai-e-vede-libras-2026-10-03", "Mesmo nome (Libras)", "libras-video.mp4")]
    public async Task DirectQuarter_ShouldParseWithoutFollowingAnotherLink(string suffix, string id, string title, string fileName)
    {
        var handler = new DirectHandler(Row("03/10"));
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(handler), () => new DateOnly(2027, 1, 1));
        var configuration = Configuration() with { PageUrl = $"https://downloads.adventistas.org/pt/possibilidades/video/provai-e-vede-2026-4o-trimestre{suffix}/?test=1" };

        var item = Assert.Single(await connector.DiscoverAsync(configuration));

        Assert.Equal(id, item.Id);
        Assert.Equal(title, item.Title);
        Assert.Equal(new DateOnly(2026, 10, 3), item.ScheduledDate);
        Assert.Equal(fileName, item.Assets.Single().FileName);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task BasePage_ShouldNotSwitchToLibrasWhenItsLinkComesFirst()
    {
        const string html = """
            <a href='/provai-e-vede-2026-3o-trimestre-libras/'>Libras</a>
            <a href='/provai-e-vede-2026-3o-trimestre-extra/'>Outro</a>
            <a href='/provai-e-vede-2026-3o-trimestre/'>Convencional</a>
            """;
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler(html)), () => new DateOnly(2026, 8, 2));
        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));
        Assert.Equal("provai-e-vede-2026-t3", item.Id);
        Assert.EndsWith("/provai-e-vede-2026-3o-trimestre/", item.PageUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("31/11")]
    [InlineData("00/10")]
    [InlineData("03/00")]
    [InlineData("03/13")]
    [InlineData("03/07")]
    public async Task DirectQuarter_ShouldIgnoreInvalidOrOutsideQuarterDates(string date)
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler(Row(date))));
        var item = Assert.Single(await connector.DiscoverAsync(DirectConfiguration()));
        Assert.Equal("provai-e-vede-libras-2026-t4", item.Id);
        Assert.Empty(item.Assets);
        Assert.Contains("Libras", item.Title);
        Assert.Equal(new DateOnly(2026, 10, 1), item.ScheduledDate);
    }

    [Fact]
    public async Task DirectQuarter_ShouldIgnoreZipAndOtherRowsAndDeduplicateVideos()
    {
        var html = "<tr><td>(01/10) Pacote 3400MB</td><td><a href='https://files.test/all.zip'>ZIP</a></td></tr>" +
            "<tr><td>Sem data</td><td><a href='https://files.test/unknown.mp4'>MP4</a></td></tr>" + Row("03/10") + Row("03/10");
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler(html)));
        var item = Assert.Single(await connector.DiscoverAsync(DirectConfiguration()));
        Assert.Equal(new DateOnly(2026, 10, 3), item.ScheduledDate);
        Assert.Equal("Mesmo nome (Libras)", item.Title);
    }

    [Theory]
    [InlineData("provai-e-vede-0000-4o-trimestre-libras")]
    [InlineData("provai-e-vede-2026-5o-trimestre-libras")]
    [InlineData("provai-e-vede-2026-4o-trimestre-libras-extra")]
    public async Task DirectQuarter_ShouldNotRecognizeMalformedSlugs(string slug)
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler("")));
        await Assert.ThrowsAsync<SiteStructureChangedException>(() => connector.DiscoverAsync(DirectConfiguration() with { PageUrl = $"https://downloads.test/{slug}/" }));
    }

    [Fact]
    public async Task DirectQuarter_ShouldRejectHttpBeforeFetching()
    {
        var handler = new DirectHandler("");
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(handler));
        await Assert.ThrowsAsync<SiteStructureChangedException>(() => connector.DiscoverAsync(DirectConfiguration() with { PageUrl = DirectConfiguration().PageUrl.Replace("https:", "http:") }));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task DirectQuarter_ShouldPropagateNetworkFailure()
    {
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new DirectHandler("", fail: true)));
        await Assert.ThrowsAsync<HttpRequestException>(() => connector.DiscoverAsync(DirectConfiguration()));
    }

    [Fact]
    public async Task DirectQuarter_ShouldHonorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new DirectHandler(Row("03/10"))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connector.DiscoverAsync(DirectConfiguration(), cancellation.Token));
    }

    [Theory]
    [InlineData(2026, 10, 27, "2026-4o", "31/10", "2026-10-31")]
    [InlineData(2027, 1, 9, "2027-1o", "09/01", "2027-01-09")]
    [InlineData(2027, 4, 3, "2027-2o", "03/04", "2027-04-03")]
    public async Task LibrasBase_ShouldSelectTheOperatingQuarterAndYearWithoutChangingItsUrl(int year, int month, int day, string slug, string date, string expectedDate)
    {
        var source = $"""
            <a href='https://downloads.example/provai-e-vede-2026-3o-trimestre-libras/'>Anterior</a>
            <a href='https://downloads.example/provai-e-vede-{slug}-trimestre/'>Convencional</a>
            <a href='http://downloads.example/provai-e-vede-{slug}-trimestre-libras/'>Inseguro</a>
            <a href='https://downloads.example/provai-e-vede-{slug}-trimestre-libras/'>Libras</a>
            """;
        var configuration = Configuration() with { PageUrl = Configuration().PageUrl + "#libras" };
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new PagesHandler(source, Row(date))), () => new DateOnly(year, month, day));

        var item = Assert.Single(await connector.DiscoverAsync(configuration));

        Assert.Equal($"provai-e-vede-libras-{expectedDate}", item.Id);
        Assert.Equal("Mesmo nome (Libras)", item.Title);
        Assert.EndsWith("#libras", configuration.PageUrl);
    }

    [Theory]
    [InlineData("#libras")]
    [InlineData("#LIBRAS")]
    public async Task LibrasBase_ShouldNeverFallBackToAnotherQuarterOrConventionalVersion(string fragment)
    {
        const string html = """
            <a href='/provai-e-vede-2026-3o-trimestre-libras/'>Anterior</a>
            <a href='/provai-e-vede-2026-4o-trimestre/'>Convencional</a>
            <a href='/provai-e-vede-2027-1o-trimestre-libras/'>Futuro</a>
            """;
        var connector = new ProvaiEVedeDiscoveryConnector(new HttpClient(new HtmlHandler(html)), () => new DateOnly(2026, 10, 27));
        var error = await Assert.ThrowsAsync<SiteStructureChangedException>(() => connector.DiscoverAsync(Configuration() with { PageUrl = Configuration().PageUrl + fragment }));
        Assert.Contains("trimestre atual em Libras", error.Message);
    }

    private static SourceConfiguration DirectConfiguration() => Configuration() with { PageUrl = "https://downloads.test/provai-e-vede-2026-4o-trimestre-libras/" };
    private static string Row(string date) => $"<tr><td>MP4 ({date}) Mesmo nome</td><td>140MB</td><td><a href='https://files.test/video.mp4'>Baixar</a></td></tr>";

    private sealed class DirectHandler(string html, bool fail = false) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (fail) throw new HttpRequestException("Falha de rede", null, HttpStatusCode.NotFound);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }

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
