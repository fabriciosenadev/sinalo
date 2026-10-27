using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class ProvaiEVedeCompatibilityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingVersions_ShouldKeepHistoryFilesAndLimitDownloadsToDiscovery(bool librasFirst)
    {
        var paths = new TestPaths(_root);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        using var client = new HttpClient(new Pages());
        var discovery = new ContentDiscoveryService([new ProvaiEVedeDiscoveryConnector(client, () => new DateOnly(2026, 10, 24))], catalog);
        var sync = new ProvaiEVedeSynchronizationService(catalog, new OfficialMediaDownloadService(client, paths), new SaturdayWindowService(), () => new DateOnly(2026, 10, 24));

        // All three entries are persisted, but only the selected Saturday is downloaded.
        var first = await discovery.RefreshAsync(Configuration(librasFirst));
        var firstReady = Assert.Single(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, true, false),
            discoveredItemIds: Ids(first)));
        await catalog.UpsertAsync([firstReady with { IsPinned = true }]);
        var playedAt = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        await catalog.RecordPlaybackAsync(firstReady.Id, playedAt);
        var originalBytes = await File.ReadAllBytesAsync(firstReady.LocalPath!);

        // Identical basenames and dates must not replace each other's files or state.
        var second = await discovery.RefreshAsync(Configuration(!librasFirst));
        var secondReady = await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: Ids(second));
        Assert.Equal(3, secondReady.Count);
        Assert.All(secondReady, item => Assert.Equal(!librasFirst, item.Id.Contains("-libras-")));
        var sameDate = Assert.Single(secondReady, item => item.ScheduledDate == firstReady.ScheduledDate);
        Assert.NotEqual(firstReady.LocalPath, sameDate.LocalPath);
        Assert.NotEqual(originalBytes, await File.ReadAllBytesAsync(sameDate.LocalPath!));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(firstReady.LocalPath!));

        var preserved = (await catalog.FindByIdAsync(firstReady.Id))!;
        Assert.Equal(firstReady.LocalPath, preserved.LocalPath);
        Assert.True(preserved.IsPinned);
        Assert.Equal(1, preserved.PlayCount);
        Assert.Equal(playedAt, preserved.FirstPlayedAtUtc);

        // Switching back retains the legacy ID and downloads only the remaining two.
        var refreshed = await discovery.RefreshAsync(Configuration(librasFirst));
        var remaining = await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: Ids(refreshed));
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, item => item.Id == firstReady.Id);
        Assert.Empty(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: Ids(refreshed)));

        // Removing a local file makes only that item eligible again.
        File.Delete(firstReady.LocalPath!);
        refreshed = await discovery.RefreshAsync(Configuration(librasFirst));
        var restored = Assert.Single(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: Ids(refreshed)));
        Assert.Equal(firstReady.Id, restored.Id);
        Assert.True(File.Exists(restored.LocalPath));
        Assert.Equal(6, (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede)).Count);
        Assert.Empty(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: new HashSet<string>()));
    }

    [Fact]
    public async Task DirectQuarter_ShouldKeepItsYearAndNeverAdvanceWithTheOperatingDate()
    {
        var paths = new TestPaths(_root);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        using var client = new HttpClient(new Pages());
        var discovery = new ContentDiscoveryService([new ProvaiEVedeDiscoveryConnector(client, () => new DateOnly(2027, 1, 9))], catalog);
        var items = await discovery.RefreshAsync(Configuration(true));
        var sync = new ProvaiEVedeSynchronizationService(catalog, new OfficialMediaDownloadService(client, paths), new SaturdayWindowService(), () => new DateOnly(2027, 1, 9));
        Assert.All(items, item => Assert.Equal(new Quarter(2026, 4), item.Quarter));
        Assert.Empty(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(true, true, true), discoveredItemIds: Ids(items)));
        Assert.Equal(3, (await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: Ids(items))).Count);
    }

    [Fact]
    public async Task LibrasBase_ShouldKeepConfigurationAndAdvanceQuarterAndYearWithoutRedownloadingHistory()
    {
        var paths = new TestPaths(_root);
        await new SinaloDatabase(paths).InitializeAsync();
        var configuration = OfficialContentPrograms.ProvaiEVede with { PageUrl = OfficialContentPrograms.ProvaiEVede.PageUrl + "#libras" };
        var settings = new SqliteConfigurationService(paths);
        await settings.SaveSourcesAsync([configuration]);
        var catalog = new SqliteContentCatalog(paths);
        var date = new DateOnly(2026, 10, 24);
        using var client = new HttpClient(new DynamicPages(() => date));
        var discovery = new ContentDiscoveryService([new ProvaiEVedeDiscoveryConnector(client, () => date)], catalog);
        var sync = new ProvaiEVedeSynchronizationService(catalog, new OfficialMediaDownloadService(client, paths), new SaturdayWindowService(), () => date);
        var downloaded = new List<ContentItem>();
        foreach (var nextDate in new[] { date, new DateOnly(2027, 1, 9), new DateOnly(2027, 4, 3) })
        {
            date = nextDate;
            var saved = (await settings.LoadSourcesAsync()).Single(item => item.Source == ContentSource.ProvaiEVede);
            Assert.Equal(configuration.PageUrl, saved.PageUrl);
            var items = await discovery.RefreshAsync(saved);
            var ready = Assert.Single(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, true, false), discoveredItemIds: Ids(items)));
            Assert.Equal($"provai-e-vede-libras-{date:yyyy-MM-dd}", ready.Id);
            downloaded.Add(ready);
            Assert.Empty(await sync.SynchronizeQuarterAsync(null, new DownloadSelection(false, false, false), discoveredItemIds: Ids(items)));
        }
        Assert.Equal(3, (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede)).Count);
        Assert.All(downloaded, item => Assert.True(File.Exists(item.LocalPath)));
    }

    private sealed class DynamicPages(Func<DateOnly> date) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var current = date();
            var quarter = Quarter.From(current);
            var slug = $"provai-e-vede-{quarter.Year}-{quarter.Number}o-trimestre";
            if (request.RequestUri!.AbsolutePath.EndsWith(".mp4"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
            var html = request.RequestUri.Host == "www.adventistas.org"
                ? $"<a href='https://downloads.test/{slug}/'>Convencional</a><a href='https://downloads.test/{slug}-libras/'>Libras</a>"
                : $"<tr><td>({current:dd/MM}) Vídeo</td><td>100 MB</td><td><a href='https://files.test/video-{current:yyyyMMdd}.mp4'>Baixar</a></td></tr>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }

    private static HashSet<string> Ids(IReadOnlyList<ContentItem> items) => items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
    private static SourceConfiguration Configuration(bool libras) => new(ContentSource.ProvaiEVede, "Provai e Vede",
        libras ? "https://downloads.test/provai-e-vede-2026-4o-trimestre-libras/" : "https://base.test/", AvailabilityPolicy.QuarterlyFull);

    private sealed class Pages : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = request.RequestUri!;
            if (uri.AbsolutePath.EndsWith(".mp4"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(uri.AbsolutePath.Contains("/libras/") ? [4, 5, 6] : [1, 2, 3]) });
            if (uri.Host == "base.test")
                return Html("<a href='https://downloads.test/provai-e-vede-2026-4o-trimestre-libras/'>Libras</a><a href='https://downloads.test/provai-e-vede-2026-4o-trimestre/'>Convencional</a>");
            var folder = uri.AbsolutePath.Contains("-libras") ? "libras" : "standard";
            return Html(string.Concat(new[] { 17, 24, 31 }.Select(day => $"<tr><td>MP4 ({day}/10) Vídeo {day}</td><td>140 MB</td><td><a href='https://files.test/{folder}/video-{day}.mp4'>Baixar</a></td></tr>")));
        }
        private static Task<HttpResponseMessage> Html(string html) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class TestPaths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp", "downloads"), Path.Combine(root, "data", "sinalo.db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders()
        {
            foreach (var path in new[] { _paths.DataPath, _paths.ContentPath, _paths.CachePath, _paths.LogsPath, _paths.TempDownloadsPath }) Directory.CreateDirectory(path);
        }
    }
}
