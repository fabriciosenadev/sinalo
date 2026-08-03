using Sinalo.Application.Catalog;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class ProvaiEVedeSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader)
{
    private readonly IContentCatalog _catalog = catalog;
    private readonly IContentDownloadService _downloader = downloader;

    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(CancellationToken cancellationToken = default)
    {
        var items = await _catalog.ListBySourceAsync(ContentSource.ProvaiEVede, cancellationToken);
        var synchronized = new List<ContentItem>();
        foreach (var item in items.Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready))
        {
            var ready = await _downloader.DownloadAsync(item, cancellationToken);
            await _catalog.UpsertAsync([ready], cancellationToken);
            synchronized.Add(ready);
        }

        return synchronized;
    }
}
