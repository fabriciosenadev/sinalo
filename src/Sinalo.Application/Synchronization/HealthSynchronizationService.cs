using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class HealthSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService saturdayWindowService, Func<DateOnly>? operatingDate = null)
{
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(AvailabilityPolicy policy, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var candidates = (await catalog.ListBySourceAsync(ContentSource.Health, cancellationToken)).Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready).ToArray();
        var selected = policy == AvailabilityPolicy.RollingSaturday ? saturdayWindowService.GetWindow(_operatingDate()).InPriorityOrder.SelectMany(date => candidates.Where(item => item.ScheduledDate == date)) : candidates;
        var ready = new List<ContentItem>();
        foreach (var item in selected)
        {
            progress?.Report(new DownloadProgress(item, 0, item.Assets.Single().ExpectedSizeBytes, "Iniciando download"));
            var downloaded = await downloader.DownloadAsync(item, progress, cancellationToken);
            await catalog.UpsertAsync([downloaded], cancellationToken);
            ready.Add(downloaded);
        }
        return ready;
    }
}
