using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Domain;
namespace Sinalo.Application.Synchronization;
public sealed class ProvaiEVedeSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService? saturdayWindowService = null, Func<DateOnly>? operatingDate = null)
{
    private readonly ISaturdayWindowService? _saturdayWindowService = saturdayWindowService;
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(IProgress<DownloadProgress>? progress = null, AvailabilityPolicy policy = AvailabilityPolicy.QuarterlyFull, CancellationToken cancellationToken = default)
        => await SynchronizeQuarterAsync(progress, DownloadSelection.FromLegacyPolicy(policy), cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(IProgress<DownloadProgress>? progress, DownloadSelection selection, CancellationToken cancellationToken = default)
    {
        var ready = new List<ContentItem>();
        var candidates = (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede, cancellationToken)).Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready).ToArray();
        var selected = !selection.DownloadsQuarterly
            ? GetPriorityDates(_operatingDate(), selection).SelectMany(date => candidates.Where(item => item.ScheduledDate == date))
            : candidates;
        foreach (var item in selected)
        {
            progress?.Report(new DownloadProgress(item, 0, item.Assets.Single().ExpectedSizeBytes, "Iniciando download"));
            var downloaded = await downloader.DownloadAsync(item, progress, cancellationToken);
            progress?.Report(new DownloadProgress(downloaded, downloaded.Assets.Single().ExpectedSizeBytes ?? 0, downloaded.Assets.Single().ExpectedSizeBytes, "Validado e disponível offline"));
            await catalog.UpsertAsync([downloaded], cancellationToken);
            ready.Add(downloaded);
        }
        return ready;
    }

    private IReadOnlyList<DateOnly> GetPriorityDates(DateOnly referenceDate, DownloadSelection selection)
    {
        if (_saturdayWindowService is not null)
        {
            var window = _saturdayWindowService.GetWindow(referenceDate);
            return [.. new[] { (window.Previous, selection.PreviousSaturday), (window.Current, selection.CurrentSaturday), (window.Next, selection.NextSaturday) }.Where(item => item.Item2).Select(item => item.Item1)];
        }
        var current = referenceDate.AddDays(-((int)referenceDate.DayOfWeek + 1) % 7);
        return [.. new[] { (current.AddDays(-7), selection.PreviousSaturday), (current, selection.CurrentSaturday), (current.AddDays(7), selection.NextSaturday) }.Where(item => item.Item2).Select(item => item.Item1)];
    }
}
