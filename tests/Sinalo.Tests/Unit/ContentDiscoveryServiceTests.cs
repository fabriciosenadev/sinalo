using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class ContentDiscoveryServiceTests
{
    private static readonly SourceConfiguration MissionsConfiguration =
        new(ContentSource.Missions, "Informativo das Missões", "https://missions.example/", AvailabilityPolicy.MonthlyFull);

    [Fact]
    public async Task RefreshAsync_ShouldSkipAnUnconfiguredSource()
    {
        var catalog = new FakeCatalog();
        var connector = new FakeConnector(ContentSource.Missions, []);
        var service = new ContentDiscoveryService([connector], catalog);

        var items = await service.RefreshAsync(MissionsConfiguration with { PageUrl = " " });

        Assert.Empty(items);
        Assert.False(connector.WasCalled);
        Assert.Empty(catalog.SavedItems);
    }

    [Fact]
    public async Task RefreshAsync_ShouldDiscoverAndPersistItemsFromTheConfiguredSource()
    {
        var item = CreateItem(ContentSource.Missions);
        var catalog = new FakeCatalog();
        var service = new ContentDiscoveryService([new FakeConnector(ContentSource.Missions, [item])], catalog);

        var items = await service.RefreshAsync(MissionsConfiguration);

        Assert.Single(items);
        Assert.Single(catalog.SavedItems);
        Assert.Equal(item, catalog.SavedItems[0]);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRejectMissingOrInvalidConnectors()
    {
        var catalog = new FakeCatalog();
        var missingConnector = new ContentDiscoveryService([], catalog);
        var invalidConnector = new ContentDiscoveryService([new FakeConnector(ContentSource.Missions, [CreateItem(ContentSource.Health)])], catalog);

        await Assert.ThrowsAsync<InvalidOperationException>(() => missingConnector.RefreshAsync(MissionsConfiguration));
        await Assert.ThrowsAsync<InvalidOperationException>(() => invalidConnector.RefreshAsync(MissionsConfiguration));
        Assert.Empty(catalog.SavedItems);
    }

    [Fact]
    public async Task RefreshAsync_ShouldKeepAnExistingReadyItemWhenItsLocalFileStillExists()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var discovered = CreateDownloadableItem();
            var existing = discovered with
            {
                SyncState = SyncState.Ready,
                LocalPath = filePath,
                PlayCount = 2
            };
            var catalog = new FakeCatalog([existing]);
            var service = new ContentDiscoveryService([new FakeConnector(ContentSource.Missions, [discovered])], catalog);

            var items = await service.RefreshAsync(MissionsConfiguration);

            var saved = Assert.Single(items);
            Assert.Equal(SyncState.Ready, saved.SyncState);
            Assert.Equal(filePath, saved.LocalPath);
            Assert.Equal(2, saved.PlayCount);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task RefreshAsync_ShouldMakeAReadyItemEligibleAgainWhenItsLocalFileWasDeleted()
    {
        var missingFilePath = Path.Combine(Path.GetTempPath(), $"Sinalo-missing-{Guid.NewGuid():N}.mp4");
        var discovered = CreateDownloadableItem();
        var existing = discovered with { SyncState = SyncState.Ready, LocalPath = missingFilePath };
        var catalog = new FakeCatalog([existing]);
        var service = new ContentDiscoveryService([new FakeConnector(ContentSource.Missions, [discovered])], catalog);

        var items = await service.RefreshAsync(MissionsConfiguration);

        var saved = Assert.Single(items);
        Assert.Equal(SyncState.Pending, saved.SyncState);
        Assert.Null(saved.LocalPath);
    }

    private static ContentItem CreateItem(ContentSource source) => new(
        "item-1", source, "Conteúdo de teste", new DateOnly(2026, 8, 8), new Uri("https://example.test/page"), [], SyncState.OnlineOnly);

    private static ContentItem CreateDownloadableItem() => new(
        "item-1", ContentSource.Missions, "Conteúdo de teste", new DateOnly(2026, 8, 8), new Uri("https://example.test/page"),
        [new MediaAsset("asset-1", new Uri("https://example.test/video.mp4"), "video.mp4", null, null)]);

    private sealed class FakeConnector(ContentSource source, IReadOnlyList<ContentItem> items) : IContentDiscoveryConnector
    {
        public ContentSource Source { get; } = source;
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(items);
        }
    }

    private sealed class FakeCatalog(IReadOnlyList<ContentItem>? existingItems = null) : IContentCatalog
    {
        public IReadOnlyList<ContentItem> SavedItems { get; private set; } = [];
        private readonly IReadOnlyList<ContentItem> _existingItems = existingItems ?? [];

        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default)
        {
            SavedItems = items;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) =>
            Task.FromResult(_existingItems.Where(item => item.Source == source).ToArray() as IReadOnlyList<ContentItem>);
    }
}
