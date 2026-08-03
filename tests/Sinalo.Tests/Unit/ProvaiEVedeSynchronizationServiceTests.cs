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
    private static ContentItem Item(string id,SyncState state,bool asset)=>new(id,ContentSource.ProvaiEVede,id,new DateOnly(2026,8,8),new Uri("https://example.test"),asset?[new MediaAsset(id,new Uri("https://example.test/a.mp4"),"a.mp4",null,null)]:[],state);
    private sealed class Catalog(IReadOnlyList<ContentItem> items):IContentCatalog { public List<ContentItem> Saved {get;}=[]; public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource s,CancellationToken c=default)=>Task.FromResult(items); public Task UpsertAsync(IReadOnlyList<ContentItem> i,CancellationToken c=default){Saved.AddRange(i);return Task.CompletedTask;} }
    private sealed class Downloader:IContentDownloadService { public List<ContentItem> Items {get;}=[]; public Task<ContentItem> DownloadAsync(ContentItem i,CancellationToken c=default){Items.Add(i);return Task.FromResult(i with {SyncState=SyncState.Ready,LocalPath="C:\\video.mp4"});} }
}
