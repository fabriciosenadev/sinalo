using System.IO;
using Microsoft.Data.Sqlite;
using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class ContentCatalogTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Catalog_ShouldPersistItemsAssetsAndOnlineOnlyState()
    {
        var paths = new TestPathService(_rootPath);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        var expected = new ContentItem(
            "missions-2026-08-08", ContentSource.Missions, "Informativo", new DateOnly(2026, 8, 8),
            new Uri("https://example.test/page"),
            [new MediaAsset("video-1", new Uri("https://example.test/video.mp4"), "video.mp4", 1024, "abc")],
            SyncState.OnlineOnly, true);

        await catalog.UpsertAsync([expected]);

        var saved = await catalog.ListBySourceAsync(ContentSource.Missions);

        var item = Assert.Single(saved);
        Assert.Equal(expected.Id, item.Id);
        Assert.Equal(SyncState.OnlineOnly, item.SyncState);
        Assert.True(item.IsPinned);
        var asset = Assert.Single(item.Assets);
        Assert.Equal("video.mp4", asset.FileName);
        Assert.Equal(1024, asset.ExpectedSizeBytes);
        Assert.Equal("abc", asset.Sha256);
        Assert.Empty(await catalog.ListBySourceAsync(ContentSource.Health));
    }

    [Fact]
    public async Task Catalog_ShouldReplaceItemAssetsWhenTheCatalogIsRefreshed()
    {
        var paths = new TestPathService(_rootPath);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        var initial = new ContentItem("health-1", ContentSource.Health, "Inicial", new DateOnly(2026, 8, 1), new Uri("https://example.test/initial"),
            [new MediaAsset("old", new Uri("https://example.test/old.mp4"), "old.mp4", null, null)]);
        var refreshed = initial with
        {
            Title = "Atualizado",
            Assets = [new MediaAsset("new", new Uri("https://example.test/new.mp4"), "new.mp4", null, null)]
        };

        await catalog.UpsertAsync([initial]);
        await catalog.UpsertAsync([refreshed]);

        var saved = Assert.Single(await catalog.ListBySourceAsync(ContentSource.Health));
        Assert.Equal("Atualizado", saved.Title);
        Assert.Equal("new", Assert.Single(saved.Assets).Id);
    }

    [Fact]
    public async Task Catalog_ShouldPersistPlaybackHistory()
    {
        var paths = new TestPathService(_rootPath);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        var item = new ContentItem("played", ContentSource.ProvaiEVede, "Reproduzido", new DateOnly(2026, 8, 8), new Uri("https://example.test/played"), [], SyncState.Ready, LocalPath: "C:\\content.mp4");
        await catalog.UpsertAsync([item]);

        var first = new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero);
        await catalog.RecordPlaybackAsync(item.Id, first);
        await catalog.RecordPlaybackAsync(item.Id, first.AddMinutes(5));

        var saved = await catalog.FindByIdAsync(item.Id);
        Assert.Equal(2, saved!.PlayCount);
        Assert.Equal(first, saved.FirstPlayedAtUtc);
        Assert.Equal(first.AddMinutes(5), saved.LastPlayedAtUtc);
    }

    [Fact]
    public async Task Catalog_ShouldDeleteTheItemAndItsAssets()
    {
        var paths = new TestPathService(_rootPath);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        var item = new ContentItem("delete", ContentSource.Missions, "Excluir", new DateOnly(2026, 8, 8), new Uri("https://example.test/delete"), [new MediaAsset("asset", new Uri("https://example.test/video.mp4"), "video.mp4", null, null)]);
        await catalog.UpsertAsync([item]);

        await catalog.DeleteAsync(item.Id);

        Assert.Null(await catalog.FindByIdAsync(item.Id));
        Assert.Empty(await catalog.ListBySourceAsync(ContentSource.Missions));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, recursive: true);
    }

    private sealed class TestPathService(string rootPath) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(rootPath, Path.Combine(rootPath, "data"), Path.Combine(rootPath, "content"), Path.Combine(rootPath, "cache"), Path.Combine(rootPath, "logs"), Path.Combine(rootPath, "temp", "downloads"), Path.Combine(rootPath, "data", "sinalo.db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders()
        {
            Directory.CreateDirectory(_paths.DataPath);
            Directory.CreateDirectory(_paths.ContentPath);
            Directory.CreateDirectory(_paths.CachePath);
            Directory.CreateDirectory(_paths.LogsPath);
            Directory.CreateDirectory(_paths.TempDownloadsPath);
        }
    }
}
