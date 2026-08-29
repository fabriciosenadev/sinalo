using Sinalo.Application.Services;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public static class SynchronizationCandidateSelector
{
    public static IReadOnlyList<ContentItem> Select(
        ContentSource source,
        IReadOnlyList<ContentItem> items,
        DownloadSelection selection,
        ISaturdayWindowService saturdayWindowService,
        DateOnly operatingDate)
    {
        var candidates = items.Where(item => item.Assets.Count > 0 && (!item.IsReadyOffline || string.IsNullOrWhiteSpace(item.LocalPath) || !File.Exists(item.LocalPath))).ToArray();
        if (selection.DownloadsQuarterly) return candidates.OrderBy(item => item.ScheduledDate).ToArray();

        var window = saturdayWindowService.GetWindow(operatingDate);
        var selectedDates = new[]
        {
            (window.Previous, selection.PreviousSaturday),
            (window.Current, selection.CurrentSaturday),
            (window.Next, selection.NextSaturday)
        }.Where(item => item.Item2).Select(item => item.Item1).ToHashSet();

        return candidates.Where(item => selectedDates.Contains(item.ScheduledDate)).OrderBy(item => item.ScheduledDate).ToArray();
    }
}
