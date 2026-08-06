using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class MissionsSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService saturdayWindowService, Func<DateOnly>? operatingDate = null)
{
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var candidates = (await catalog.ListBySourceAsync(ContentSource.Missions, cancellationToken))
            .Where(item => item.Assets.Count > 0 && item.SyncState != SyncState.Ready)
            .ToArray();
        var selected = SelectItemsToSynchronize(candidates, _operatingDate(), saturdayWindowService);
        var ready = new List<ContentItem>();

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

    public static IReadOnlyList<ContentItem> SelectItemsToSynchronize(IReadOnlyList<ContentItem> items, DateOnly referenceDate, ISaturdayWindowService saturdayWindowService)
    {
        var monthItems = items.Where(item => item.ScheduledDate.Year == referenceDate.Year && item.ScheduledDate.Month == referenceDate.Month).ToArray();
        var monthSaturdays = SaturdaysInMonth(referenceDate.Year, referenceDate.Month).ToArray();
        var completeMonth = monthSaturdays.Length > 0 && monthSaturdays.All(date => monthItems.Any(item => item.ScheduledDate == date));
        if (completeMonth) return monthItems.OrderBy(item => item.ScheduledDate).ToArray();

        var priorities = saturdayWindowService.GetWindow(referenceDate).InPriorityOrder;
        return priorities.SelectMany(date => items.Where(item => item.ScheduledDate == date)).ToArray();
    }

    private static IEnumerable<DateOnly> SaturdaysInMonth(int year, int month)
    {
        var date = new DateOnly(year, month, 1);
        while (date.Month == month)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday) yield return date;
            date = date.AddDays(1);
        }
    }
}
