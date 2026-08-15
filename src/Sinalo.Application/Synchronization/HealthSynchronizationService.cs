using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class HealthSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService saturdayWindowService, Func<DateOnly>? operatingDate = null)
{
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(AvailabilityPolicy policy, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        => await SynchronizeAsync(DownloadSelection.FromLegacyPolicy(policy), progress, cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(DownloadSelection selection, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var candidates = (await catalog.ListBySourceAsync(ContentSource.Health, cancellationToken)).Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready).ToArray();
        var selected = selection.DownloadsQuarterly
            ? candidates
            : GetSelectedDates(saturdayWindowService.GetWindow(_operatingDate()), selection).SelectMany(date => candidates.Where(item => item.ScheduledDate == date));
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

    private static IReadOnlyList<DateOnly> GetSelectedDates(SaturdayWindow window, DownloadSelection selection) =>
        [.. new[] { (window.Previous, selection.PreviousSaturday), (window.Current, selection.CurrentSaturday), (window.Next, selection.NextSaturday) }.Where(item => item.Item2).Select(item => item.Item1)];
}
