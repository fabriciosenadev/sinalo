using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Domain;
namespace Sinalo.Application.Synchronization;
public sealed class ProvaiEVedeSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService? saturdayWindowService = null, Func<DateOnly>? operatingDate = null)
{
    private readonly ISaturdayWindowService? _saturdayWindowService = saturdayWindowService;
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(IProgress<DownloadProgress>? progress = null, AvailabilityPolicy policy = AvailabilityPolicy.QuarterlyFull, CancellationToken cancellationToken = default)
    {
        var ready = new List<ContentItem>();
        var candidates = (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede, cancellationToken)).Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready).ToArray();
        var selected = policy == AvailabilityPolicy.RollingSaturday
            ? GetPriorityDates(_operatingDate()).SelectMany(date => candidates.Where(item => item.ScheduledDate == date))
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

    private IReadOnlyList<DateOnly> GetPriorityDates(DateOnly referenceDate)
    {
        if (_saturdayWindowService is not null) return _saturdayWindowService.GetWindow(referenceDate).InPriorityOrder;
        var current = referenceDate.AddDays(-((int)referenceDate.DayOfWeek + 1) % 7);
        return [current.AddDays(-7), current, current.AddDays(7)];
    }
}
