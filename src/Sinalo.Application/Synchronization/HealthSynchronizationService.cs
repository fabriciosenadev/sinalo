using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class HealthSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService saturdayWindowService, Func<DateOnly>? operatingDate = null, IContentStorageSpaceService? storageSpaceService = null)
{
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(AvailabilityPolicy policy, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        => await SynchronizeAsync(DownloadSelection.FromLegacyPolicy(policy), progress, cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(DownloadSelection selection, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var selected = SynchronizationCandidateSelector.Select(ContentSource.Health, await catalog.ListBySourceAsync(ContentSource.Health, cancellationToken), selection, saturdayWindowService, _operatingDate());
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
