using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
namespace Sinalo.Tests.Unit;
public sealed class ProvaiEVedeSynchronizationServiceTests
{
    [Fact] public async Task SynchronizeQuarterAsync_ShouldOnlyDownloadPendingItemsWithAssets()
    {
        var pending=Item("pending",SyncState.Pending,true); var ready=Item("ready",SyncState.Ready,true); var noAsset=Item("none",SyncState.Pending,false); var catalog=new Catalog([pending,ready,noAsset]); var downloader=new Downloader();
        var result=await new ProvaiEVedeSynchronizationService(catalog,downloader).SynchronizeQuarterAsync();
        Assert.Equal(2, result.Count); Assert.Equal(2, downloader.Items.Count); Assert.Equal(2, catalog.Saved.Count);
    }

    [Fact] public async Task SynchronizeQuarterAsync_ShouldReportItemAvailabilityAfterEachDownload()
    {
        var pending = Item("pending", SyncState.Pending, true);
        var catalog = new Catalog([pending]);
        var events = new List<DownloadProgress>();

        await new ProvaiEVedeSynchronizationService(catalog, new Downloader()).SynchronizeQuarterAsync(new InlineProgress(events));

        Assert.Contains(events, item => item.Item.Id == "pending" && item.Stage == "Iniciando download");
        Assert.Contains(events, item => item.Item.Id == "pending" && item.Stage == "Validado e disponível offline");
    }
    [Fact] public async Task SynchronizeQuarterAsync_ShouldUseTheSaturdayWindowWhenConfigured()
    {
        var catalog = new Catalog([Item("old", SyncState.Pending, true, new DateOnly(2026, 7, 25)), Item("previous", SyncState.Pending, true, new DateOnly(2026, 8, 1)), Item("current", SyncState.Pending, true, new DateOnly(2026, 8, 8)), Item("next", SyncState.Pending, true, new DateOnly(2026, 8, 15)), Item("later", SyncState.Pending, true, new DateOnly(2026, 8, 22))]);
        var downloader = new Downloader();

        await new ProvaiEVedeSynchronizationService(catalog, downloader, operatingDate: () => new DateOnly(2026, 8, 8)).SynchronizeQuarterAsync(policy: AvailabilityPolicy.RollingSaturday);

        Assert.Equal(["previous", "current", "next"], downloader.Items.Select(item => item.Id));
    }
    [Fact] public async Task SynchronizeQuarterAsync_ShouldOnlyDownloadTheExplicitlySelectedSaturdays()
    {
        var catalog = new Catalog([Item("previous", SyncState.Pending, true, new DateOnly(2026, 8, 1)), Item("current", SyncState.Pending, true, new DateOnly(2026, 8, 8)), Item("next", SyncState.Pending, true, new DateOnly(2026, 8, 15))]);
        var downloader = new Downloader();

        await new ProvaiEVedeSynchronizationService(catalog, downloader, operatingDate: () => new DateOnly(2026, 8, 8))
            .SynchronizeQuarterAsync(null, new DownloadSelection(false, true, false));

        Assert.Equal(["current"], downloader.Items.Select(item => item.Id));
    }
    [Fact] public async Task SynchronizeQuarterAsync_ShouldStopBeforeDownloadingWhenTheInitialSpaceCheckFails()
    {
        var catalog = new Catalog([Item("pending", SyncState.Pending, true)]);
        var downloader = new Downloader();
        var space = new SpaceService(false);

        await Assert.ThrowsAsync<InsufficientStorageSpaceException>(() => new ProvaiEVedeSynchronizationService(catalog, downloader, storageSpaceService: space).SynchronizeQuarterAsync());

        Assert.Empty(downloader.Items);
        Assert.Equal(1, space.Assessments);
    }
    [Fact] public async Task SynchronizeQuarterAsync_ShouldRevalidateSpaceBeforeEveryItem()
    {
        var catalog = new Catalog([Item("first", SyncState.Pending, true), Item("second", SyncState.Pending, true)]);
        var downloader = new Downloader();
        var space = new SpaceService(true);

        await new ProvaiEVedeSynchronizationService(catalog, downloader, storageSpaceService: space).SynchronizeQuarterAsync();

        Assert.Equal(3, space.Assessments);
        Assert.Equal(2, downloader.Items.Count);
    }
    private static ContentItem Item(string id,SyncState state,bool asset, DateOnly? date = null)=>new(id,ContentSource.ProvaiEVede,id,date ?? new DateOnly(2026,8,8),new Uri("https://example.test"),asset?[new MediaAsset(id,new Uri("https://example.test/a.mp4"),"a.mp4",null,null)]:[],state);
    private sealed class Catalog(IReadOnlyList<ContentItem> items):IContentCatalog { public List<ContentItem> Saved {get;}=[]; public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource s,CancellationToken c=default)=>Task.FromResult(items); public Task UpsertAsync(IReadOnlyList<ContentItem> i,CancellationToken c=default){Saved.AddRange(i);return Task.CompletedTask;} }
    private sealed class Downloader:IContentDownloadService { public List<ContentItem> Items {get;}=[]; public Task<ContentItem> DownloadAsync(ContentItem i, IProgress<DownloadProgress>? progress = null, CancellationToken c=default){Items.Add(i); progress?.Report(new DownloadProgress(i, 1, 1, "Disponível offline")); return Task.FromResult(i with {SyncState=SyncState.Ready,LocalPath="C:\\video.mp4"});} }
    private sealed class InlineProgress(List<DownloadProgress> events) : IProgress<DownloadProgress> { public void Report(DownloadProgress value) => events.Add(value); }
    private sealed class SpaceService(bool sufficient) : IContentStorageSpaceService
    {
        public int Assessments { get; private set; }
        public Task<ContentStorageSpaceAssessment> AssessAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default)
        {
            Assessments++;
            return Task.FromResult(new ContentStorageSpaceAssessment("C:\\", sufficient ? 2 : 1, 1, 2, 0));
        }
        public Task<bool> HasMinimumFreeSpaceAsync(string path, long minimumFreeBytes, CancellationToken cancellationToken = default) => Task.FromResult(sufficient);
    }
}
