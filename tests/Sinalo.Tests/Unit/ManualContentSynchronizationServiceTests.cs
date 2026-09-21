using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class ManualContentSynchronizationServiceTests
{
    [Fact]
    public async Task SynchronizeAsync_ShouldDownloadOnlyTheFrozenSelectedItems()
    {
        var selected = Item("selected", new DateOnly(2026, 9, 5));
        var notSelected = Item("not-selected", new DateOnly(2026, 9, 12));
        var offline = Item("offline", new DateOnly(2026, 9, 19)) with { SyncState = SyncState.Ready, LocalPath = Environment.ProcessPath };
        var catalog = new Catalog([selected, notSelected, offline]);
        var downloader = new Downloader();

        var result = await new ManualContentSynchronizationService(catalog, downloader)
            .SynchronizeAsync(ContentSource.Missions, ["selected", "offline"]);

        Assert.Equal(["selected"], downloader.DownloadedIds);
        Assert.Equal(["selected"], result.Select(item => item.Id));
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldIgnoreItemsThatNoLongerHaveAnOfficialFile()
    {
        var available = Item("available", new DateOnly(2026, 9, 5), ContentSource.Health);
        var removed = Item("removed", new DateOnly(2026, 9, 12), ContentSource.Health) with { Assets = [] };
        var downloader = new Downloader();

        await new ManualContentSynchronizationService(new Catalog([available, removed]), downloader)
            .SynchronizeAsync(ContentSource.Health, ["available", "removed"]);

        Assert.Equal(["available"], downloader.DownloadedIds);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldStopBeforeDownloadWhenSpaceIsInsufficient()
    {
        var item = Item("large", new DateOnly(2026, 9, 5));
        var storage = new Storage(new ContentStorageSpaceAssessment("C:", 10, 100, 100, 0));

        await Assert.ThrowsAsync<InsufficientStorageSpaceException>(() => new ManualContentSynchronizationService(new Catalog([item]), new Downloader(), storage)
            .SynchronizeAsync(ContentSource.Missions, ["large"]));
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldReportUnknownSizesWhenSpaceIsSufficient()
    {
        var item = Item("unknown", new DateOnly(2026, 9, 5)) with { Assets = [new MediaAsset("unknown", new Uri("https://example.test/unknown.mp4"), "unknown.mp4", null, null)] };
        var reports = new List<DownloadProgress>();
        var storage = new Storage(new ContentStorageSpaceAssessment("C:", 1000, 0, 0, 1));

        await new ManualContentSynchronizationService(new Catalog([item]), new Downloader(), storage)
            .SynchronizeAsync(ContentSource.Missions, ["unknown"], new InlineProgress(reports));

        Assert.Contains(reports, report => report.Stage.Contains("Tamanho não informado", StringComparison.Ordinal));
    }

    private static ContentItem Item(string id, DateOnly date, ContentSource source = ContentSource.Missions) => new(id, source, id, date, new Uri("https://example.test"), [new MediaAsset(id, new Uri($"https://example.test/{id}.mp4"), $"{id}.mp4", 100, null)]);

    private sealed class Catalog(IReadOnlyList<ContentItem> items) : IContentCatalog
    {
        private readonly List<ContentItem> _items = items.ToList();
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>(_items.Where(item => item.Source == source).ToArray());
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) { foreach (var item in items) { var index = _items.FindIndex(current => current.Id == item.Id); if (index >= 0) _items[index] = item; } return Task.CompletedTask; }
    }

    private sealed class Downloader : IContentDownloadService
    {
        public List<string> DownloadedIds { get; } = [];
        public Task<ContentItem> DownloadAsync(ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            DownloadedIds.Add(item.Id);
            return Task.FromResult(item with { SyncState = SyncState.Ready, LocalPath = Environment.ProcessPath });
        }
    }

    private sealed class Storage(ContentStorageSpaceAssessment assessment) : IContentStorageSpaceService
    {
        public Task<ContentStorageSpaceAssessment> AssessAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.FromResult(assessment);
        public Task<bool> HasMinimumFreeSpaceAsync(string path, long minimumFreeBytes, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class InlineProgress(List<DownloadProgress> reports) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => reports.Add(value);
    }
}
