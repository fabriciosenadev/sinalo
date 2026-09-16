using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class LocalContentCleanupService(
    IContentCatalog catalog,
    ISinaloPathService paths,
    IContentCleanupConfigurationService configurationService) : IContentCleanupService
{
    public async Task<ContentCleanupResult> CleanIfDueAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.LoadAsync(cancellationToken);
        if (!configuration.IsEnabled || HasAlreadyRunThisMonth(configuration.LastRunDate, today)) return ContentCleanupResult.NotDue;

        var cutoff = today.AddMonths(-configuration.NormalizedRetentionMonths).AddDays(-configuration.NormalizedGracePeriodDays);
        var root = Path.GetFullPath(paths.GetPaths().ContentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var removed = 0;
        long reclaimed = 0;
        var pinned = 0;

        foreach (var source in Enum.GetValues<ContentSource>())
        foreach (var item in await catalog.ListBySourceAsync(source, cancellationToken))
        {
            if (!item.IsReadyOffline || item.ScheduledDate >= cutoff) continue;
            if (item.IsPinned) { pinned++; continue; }
            if (string.IsNullOrWhiteSpace(item.LocalPath)) continue;

            var filePath = Path.GetFullPath(item.LocalPath);
            if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

            if (File.Exists(filePath))
            {
                var length = new FileInfo(filePath).Length;
                File.Delete(filePath);
                reclaimed += length;
            }
            await catalog.DeleteAsync(item.Id, cancellationToken);
            removed++;
        }

        await configurationService.SaveAsync(configuration with { LastRunDate = today }, cancellationToken);
        return new ContentCleanupResult(removed, reclaimed, pinned, true);
    }

    private static bool HasAlreadyRunThisMonth(DateOnly? lastRun, DateOnly today) =>
        lastRun is { } date && date.Year == today.Year && date.Month == today.Month;
}
