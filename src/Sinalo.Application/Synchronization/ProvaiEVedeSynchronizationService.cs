using Sinalo.Application.Catalog;
using Sinalo.Domain;
namespace Sinalo.Application.Synchronization;
public sealed class ProvaiEVedeSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader)
{
    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(CancellationToken cancellationToken = default)
    {
        var ready = new List<ContentItem>();
        foreach (var item in (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede, cancellationToken)).Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready))
        { var downloaded = await downloader.DownloadAsync(item, cancellationToken); await catalog.UpsertAsync([downloaded], cancellationToken); ready.Add(downloaded); }
        return ready;
    }
}
