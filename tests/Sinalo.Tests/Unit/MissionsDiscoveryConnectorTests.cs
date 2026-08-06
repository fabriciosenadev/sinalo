using System.Net;
using System.Net.Http;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class MissionsDiscoveryConnectorTests
{
    [Fact]
    public async Task DiscoverAsync_ShouldFindMissionPostsAndTheirOfficialVideos()
    {
        const string home = "<a href='/informativo-mundial/3o-trimestre-2026/'>3º Trimestre 2026</a>";
        const string quarter = """
            <a href='/2026/08/informativo-mundial-das-missoes-08-agosto-2026/'>Informativo Mundial das Missões | 08 AGOSTO 2026</a>
            <a href='/2026/08/informativo-alternativo-08-agosto-2026/'>Informativo Alternativo | 08 AGOSTO 2026</a>
            <a href='/2026/08/informativo-infantil-08-agosto-2026/'>Informativo Infantil | 08 AGOSTO 2026</a>
            """;
        const string post = "<a href='https://files.example/missions-08-08.mp4'>Baixar vídeo</a>";
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler(home, quarter, post)), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal(ContentSource.Missions, item.Source);
        Assert.Equal(new DateOnly(2026, 8, 8), item.ScheduledDate);
        Assert.Contains("Informativo Mundial", item.Title);
        Assert.Equal("https://files.example/missions-08-08.mp4", item.Assets.Single().DownloadUri.AbsoluteUri);
        Assert.Equal(SyncState.Pending, item.SyncState);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldKeepAPublishedPostOnlineWhenNoOfficialFileExists()
    {
        const string home = "<a href='/informativo-mundial/3o-trimestre-2026/'>3º Trimestre 2026</a>";
        const string quarter = "<a href='/2026/08/informativo-mundial-das-missoes-08-agosto-2026/'>Informativo Mundial das Missões | 08 AGOSTO 2026</a>";
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler(home, quarter, "<p>Vídeo somente online</p>")), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Empty(item.Assets);
        Assert.Equal(SyncState.OnlineOnly, item.SyncState);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldReadTheDateFromTheQuarterPageParagraphWhenTheLinkTextIsOnlyDownload()
    {
        const string home = "<a href='/informativo-mundial/3o-trimestre-2026/'>3º Trimestre 2026</a>";
        const string quarter = "<p><strong>6 – O sonho de Enoc – Parte 1 – </strong>08/08/26: <a href='/2026/08/informativo-mundial-das-missoes-08-agosto-2026/'>Download</a></p>";
        const string post = "<a class='kcc-link' href='/?download=1&amp;kccpid=35730&amp;redirlink=74689'>Download: Vídeo em Português 1920x1080p (31,1 MB)</a>";
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler(home, quarter, post)), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal(new DateOnly(2026, 8, 8), item.ScheduledDate);
        Assert.Equal("O sonho de Enoc – Parte 1", item.Title);
        Assert.Equal(SyncState.Pending, item.SyncState);
        Assert.Single(item.Assets);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldReturnNoItemsWhenTheCurrentQuarterIsUnavailable()
    {
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler("<a href='/informativo-mundial/2o-trimestre-2026/'>2º Trimestre 2026</a>", "", "")), () => new DateOnly(2026, 8, 2));

        Assert.Empty(await connector.DiscoverAsync(Configuration()));
    }

    [Fact]
    public async Task DiscoverAsync_ShouldKeepTheQuarterMarkerWhenNoMissionPostIsPublishedYet()
    {
        const string home = "<a href='/informativo-mundial/3o-trimestre-2026/'>3º Trimestre 2026</a>";
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler(home, "<p>Sem postagens</p>", "")), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Empty(item.Assets);
        Assert.Contains("3º Trimestre", item.Title);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldIgnoreInsecureDownloadLinks()
    {
        const string home = "<a href='/informativo-mundial/3o-trimestre-2026/'>3º Trimestre 2026</a>";
        const string quarter = "<a href='/2026/08/informativo-mundial-das-missoes-08-agosto-2026/'>Informativo Mundial das Missões | 08 AGOSTO 2026</a>";
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler(home, quarter, "<a href='http://files.example/missions.mp4'>Baixar</a>")), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal(SyncState.OnlineOnly, item.SyncState);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldChooseTheHighestResolutionPortugueseVideoFromTheOfficialDownloadLinks()
    {
        const string home = "<a href='/informativo-mundial/3o-trimestre-2026/'>3º Trimestre 2026</a>";
        const string quarter = "<a href='/2026/08/informativo-mundial-das-missoes-08-agosto-2026/'>Informativo Mundial das Missões | 08 AGOSTO 2026</a>";
        const string post = """
            <a class='kcc-link' href='/?download=1&amp;kccpid=35730&amp;redirlink=74690'>Download: Vídeo em Português 640x360p (7,2 MB)</a>
            <a class='kcc-link' href='/?download=1&amp;kccpid=35730&amp;redirlink=74689'>Download: Vídeo em Português 1920x1080p (31,1 MB)</a>
            <a class='kcc-link' href='/?download=1&amp;kccpid=35730&amp;redirlink=74691'>Download: Vídeo em Espanhol 1920x1080p</a>
            """;
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler(home, quarter, post)), () => new DateOnly(2026, 8, 2));

        var item = Assert.Single(await connector.DiscoverAsync(Configuration()));

        Assert.Equal(SyncState.Pending, item.SyncState);
        Assert.Contains("download=1", item.Assets.Single().DownloadUri.Query);
        Assert.Contains("redirlink=74689", item.Assets.Single().DownloadUri.Query);
        Assert.EndsWith(".mp4", item.Assets.Single().FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldRejectAnotherSource()
    {
        var connector = new MissionsDiscoveryConnector(new HttpClient(new PagesHandler("", "", "")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.DiscoverAsync(Configuration() with { Source = ContentSource.Health }));
    }

    private static SourceConfiguration Configuration() => new(ContentSource.Missions, "Informativo das Missões", "https://www.daniellocutor.com.br/", AvailabilityPolicy.MonthlyFull);

    private sealed class PagesHandler(string home, string quarter, string post) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var html = path.Contains("3o-trimestre", StringComparison.OrdinalIgnoreCase) ? quarter : path.Contains("informativo-mundial-das-missoes", StringComparison.OrdinalIgnoreCase) ? post : home;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }
}
