using Sinalo.Application.Catalog;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
namespace Sinalo.Tests.Unit;
public sealed class ProvaiEVedeSynchronizationServiceTests
{
    [Fact] public async Task SynchronizeQuarterAsync_ShouldOnlyDownloadPendingItemsWithAssets()
    {
        var pending=Item("pending",SyncState.Pending,true); var ready=Item("ready",SyncState.Ready,true); var noAsset=Item("none",SyncState.Pending,false); var catalog=new Catalog([pending,ready,noAsset]); var downloader=new Downloader();
        var result=await new ProvaiEVedeSynchronizationService(catalog,downloader).SynchronizeQuarterAsync();
        Assert.Single(result); Assert.Single(downloader.Items); Assert.Single(catalog.Saved);
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
    private static ContentItem Item(string id,SyncState state,bool asset)=>new(id,ContentSource.ProvaiEVede,id,new DateOnly(2026,8,8),new Uri("https://example.test"),asset?[new MediaAsset(id,new Uri("https://example.test/a.mp4"),"a.mp4",null,null)]:[],state);
    private sealed class Catalog(IReadOnlyList<ContentItem> items):IContentCatalog { public List<ContentItem> Saved {get;}=[]; public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource s,CancellationToken c=default)=>Task.FromResult(items); public Task UpsertAsync(IReadOnlyList<ContentItem> i,CancellationToken c=default){Saved.AddRange(i);return Task.CompletedTask;} }
    private sealed class Downloader:IContentDownloadService { public List<ContentItem> Items {get;}=[]; public Task<ContentItem> DownloadAsync(ContentItem i, IProgress<DownloadProgress>? progress = null, CancellationToken c=default){Items.Add(i); progress?.Report(new DownloadProgress(i, 1, 1, "Disponível offline")); return Task.FromResult(i with {SyncState=SyncState.Ready,LocalPath="C:\\video.mp4"});} }
    private sealed class InlineProgress(List<DownloadProgress> events) : IProgress<DownloadProgress> { public void Report(DownloadProgress value) => events.Add(value); }
}
