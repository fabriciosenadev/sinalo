using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class ManualContentSynchronizationService(
    IContentCatalog catalog,
    IContentDownloadService downloader,
    IContentStorageSpaceService? storageSpaceService = null)
{
    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(
        ContentSource source,
        IReadOnlyList<string> selectedItemIds,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var identifiers = selectedItemIds.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var selected = (await catalog.ListBySourceAsync(source, cancellationToken))
            .Where(item => identifiers.Contains(item.Id))
            .Where(item => item.Assets.Count > 0)
            .Where(item => !item.IsReadyOffline || string.IsNullOrWhiteSpace(item.LocalPath) || !File.Exists(item.LocalPath))
            .OrderBy(item => item.ScheduledDate)
            .ToArray();

        await EnsureSpaceAsync(selected, progress, cancellationToken);
        var ready = new List<ContentItem>();
        foreach (var item in selected)
        {
            await EnsureSpaceAsync([item], progress, cancellationToken);
            progress?.Report(new DownloadProgress(item, 0, item.Assets.Single().ExpectedSizeBytes, "Iniciando download"));
            var downloaded = await downloader.DownloadAsync(item, progress, cancellationToken);
            await catalog.UpsertAsync([downloaded], cancellationToken);
            ready.Add(downloaded);
        }

        return ready;
    }

    private async Task EnsureSpaceAsync(IReadOnlyList<ContentItem> items, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (storageSpaceService is null || items.Count == 0) return;
        var assessment = await storageSpaceService.AssessAsync(items, cancellationToken);
        if (!assessment.HasSufficientSpace) throw new InsufficientStorageSpaceException(assessment);
        if (assessment.HasUnknownSizes) progress?.Report(new DownloadProgress(items[0], 0, null, "Tamanho não informado; acompanhando espaço em disco"));
    }
}
