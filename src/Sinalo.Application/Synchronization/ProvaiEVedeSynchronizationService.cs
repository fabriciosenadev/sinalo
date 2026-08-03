using Sinalo.Application.Catalog;
using Sinalo.Domain;
namespace Sinalo.Application.Synchronization;
public sealed class ProvaiEVedeSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader)
{
    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var ready = new List<ContentItem>();
        foreach (var item in (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede, cancellationToken)).Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready))
        {
            progress?.Report(new DownloadProgress(item, 0, item.Assets.Single().ExpectedSizeBytes, "Iniciando download"));
            var downloaded = await downloader.DownloadAsync(item, progress, cancellationToken);
            progress?.Report(new DownloadProgress(downloaded, downloaded.Assets.Single().ExpectedSizeBytes ?? 0, downloaded.Assets.Single().ExpectedSizeBytes, "Validado e disponível offline"));
            await catalog.UpsertAsync([downloaded], cancellationToken);
            ready.Add(downloaded);
        }
        return ready;
    }
}
