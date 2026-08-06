using Sinalo.Application.Catalog;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class HealthSynchronizationServiceTests
{
    [Fact]
    public async Task SynchronizeAsync_ShouldUseOnlyTheSaturdayWindowWhenConfigured()
    {
        var catalog = new Catalog([Item("old", new DateOnly(2026, 7, 25)), Item("previous", new DateOnly(2026, 8, 1)), Item("current", new DateOnly(2026, 8, 8)), Item("next", new DateOnly(2026, 8, 15)), Item("later", new DateOnly(2026, 8, 22))]);
        var downloader = new Downloader();

        await new HealthSynchronizationService(catalog, downloader, new SaturdayWindowService(), () => new DateOnly(2026, 8, 8)).SynchronizeAsync(AvailabilityPolicy.RollingSaturday);

        Assert.Equal(["previous", "current", "next"], downloader.Downloaded.Select(item => item.Id));
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldDownloadTheCompleteQuarterWhenConfigured()
    {
        var catalog = new Catalog([Item("first", new DateOnly(2026, 7, 4)), Item("second", new DateOnly(2026, 8, 8))]);
        var downloader = new Downloader();

        await new HealthSynchronizationService(catalog, downloader, new SaturdayWindowService(), () => new DateOnly(2026, 8, 8)).SynchronizeAsync(AvailabilityPolicy.QuarterlyFull);

        Assert.Equal(2, downloader.Downloaded.Count);
    }

    private static ContentItem Item(string id, DateOnly date) => new(id, ContentSource.Health, id, date, new Uri("https://example.test/" + id), [new MediaAsset(id, new Uri("https://files.example/" + id + ".mp4"), id + ".mp4", null, null)]);
    private sealed class Catalog(IReadOnlyList<ContentItem> items) : IContentCatalog { public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult(items); public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Downloader : IContentDownloadService { public List<ContentItem> Downloaded { get; } = []; public Task<ContentItem> DownloadAsync(ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) { Downloaded.Add(item); return Task.FromResult(item with { SyncState = SyncState.Ready, LocalPath = "C:\\health.mp4" }); } }
}
