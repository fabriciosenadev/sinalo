using Sinalo.Application.Catalog;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class ProvaiEVedeSynchronizationServiceTests
{
    [Fact]
    public async Task SynchronizeQuarterAsync_ShouldDownloadOnlyPendingItemsWithOfficialAssets()
    {
        var pending = Item("pending", SyncState.Pending, true);
        var ready = Item("ready", SyncState.Ready, true);
        var online = Item("online", SyncState.OnlineOnly, false);
        var catalog = new Catalog([pending, ready, online]);
        var downloader = new Downloader();
        var service = new ProvaiEVedeSynchronizationService(catalog, downloader);

        var synchronized = await service.SynchronizeQuarterAsync();

        var item = Assert.Single(synchronized);
        Assert.Equal("pending", item.Id);
        Assert.Equal(SyncState.Ready, item.SyncState);
        Assert.Single(downloader.Downloaded);
        Assert.Single(catalog.Saved);
    }

    private static ContentItem Item(string id, SyncState state, bool withAsset) => new(
        id, ContentSource.ProvaiEVede, id, new DateOnly(2026, 8, 8), new Uri("https://example.test/page"),
        withAsset ? [new MediaAsset($"asset-{id}", new Uri("https://example.test/video.mp4"), "video.mp4", null, null)] : [], state);

    private sealed class Catalog(IReadOnlyList<ContentItem> items) : IContentCatalog
    {
        public List<ContentItem> Saved { get; } = [];
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult(items);
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) { Saved.AddRange(items); return Task.CompletedTask; }
    }

    private sealed class Downloader : IContentDownloadService
    {
        public List<ContentItem> Downloaded { get; } = [];
        public Task<ContentItem> DownloadAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            Downloaded.Add(item);
            return Task.FromResult(item with { SyncState = SyncState.Ready, LocalPath = "C:\\content\\video.mp4" });
        }
    }
}
