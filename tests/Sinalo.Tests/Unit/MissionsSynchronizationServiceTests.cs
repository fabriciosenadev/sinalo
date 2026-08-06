using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class MissionsSynchronizationServiceTests
{
    [Fact]
    public void SelectItemsToSynchronize_ShouldPreferTheThreeSaturdayWindowWhenMonthIsIncomplete()
    {
        var items = new[] { Item(1), Item(8), Item(15), Item(22) };

        var selected = MissionsSynchronizationService.SelectItemsToSynchronize(items, new DateOnly(2026, 8, 3), new SaturdayWindowService());

        Assert.Equal([new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 8), new DateOnly(2026, 8, 15)], selected.Select(item => item.ScheduledDate));
    }

    [Fact]
    public void SelectItemsToSynchronize_ShouldSelectTheCompleteCurrentMonth()
    {
        var items = new[] { Item(1), Item(8), Item(15), Item(22), Item(29) };

        var selected = MissionsSynchronizationService.SelectItemsToSynchronize(items, new DateOnly(2026, 8, 3), new SaturdayWindowService());

        Assert.Equal(5, selected.Count);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldOnlyDownloadTheSelectedMissionItems()
    {
        var catalog = new Catalog([Item(1), Item(8), Item(15), Item(22)]);
        var downloader = new Downloader();
        var service = new MissionsSynchronizationService(catalog, downloader, new SaturdayWindowService(), () => new DateOnly(2026, 8, 3));

        await service.SynchronizeAsync();

        Assert.Equal(3, downloader.Items.Count);
        Assert.Equal(3, catalog.Saved.Count);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldSkipReadyAndOnlineOnlyItems()
    {
        var ready = Item(1) with { SyncState = SyncState.Ready };
        var onlineOnly = Item(8) with { Assets = [], SyncState = SyncState.OnlineOnly };
        var downloader = new Downloader();
        var service = new MissionsSynchronizationService(new Catalog([ready, onlineOnly]), downloader, new SaturdayWindowService(), () => new DateOnly(2026, 8, 3));

        var result = await service.SynchronizeAsync();

        Assert.Empty(result);
        Assert.Empty(downloader.Items);
    }

    private static ContentItem Item(int day) => new($"missions-2026-08-{day:00}", ContentSource.Missions, "Informativo", new DateOnly(2026, 8, day), new Uri("https://example.test/post"), [new MediaAsset($"asset-{day}", new Uri($"https://example.test/{day}.mp4"), $"{day}.mp4", null, null)]);

    private sealed class Catalog(IReadOnlyList<ContentItem> items) : IContentCatalog
    {
        public List<ContentItem> Saved { get; } = [];
        public Task UpsertAsync(IReadOnlyList<ContentItem> values, CancellationToken cancellationToken = default) { Saved.AddRange(values); return Task.CompletedTask; }
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult(items);
    }

    private sealed class Downloader : IContentDownloadService
    {
        public List<ContentItem> Items { get; } = [];
        public Task<ContentItem> DownloadAsync(ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.FromResult(item with { SyncState = SyncState.Ready, LocalPath = "C:\\video.mp4" });
        }
    }
}
