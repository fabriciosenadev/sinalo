using Sinalo.Application.Catalog;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Domain;
namespace Sinalo.Application.Synchronization;
public sealed class ProvaiEVedeSynchronizationService(IContentCatalog catalog, IContentDownloadService downloader, ISaturdayWindowService? saturdayWindowService = null, Func<DateOnly>? operatingDate = null, IContentStorageSpaceService? storageSpaceService = null)
{
    private readonly ISaturdayWindowService? _saturdayWindowService = saturdayWindowService;
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(IProgress<DownloadProgress>? progress = null, AvailabilityPolicy policy = AvailabilityPolicy.QuarterlyFull, CancellationToken cancellationToken = default)
        => await SynchronizeQuarterAsync(progress, DownloadSelection.FromLegacyPolicy(policy), cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> SynchronizeQuarterAsync(IProgress<DownloadProgress>? progress, DownloadSelection selection, CancellationToken cancellationToken = default)
    {
        var ready = new List<ContentItem>();
        var candidates = (await catalog.ListBySourceAsync(ContentSource.ProvaiEVede, cancellationToken))
            .Where(item => item.Assets.Count > 0 && (!item.IsReadyOffline || string.IsNullOrWhiteSpace(item.LocalPath) || !File.Exists(item.LocalPath)))
            .ToArray();
        var selected = _saturdayWindowService is null
            ? SelectWithoutWindowService(candidates, selection)
            : SynchronizationCandidateSelector.Select(ContentSource.ProvaiEVede, candidates, selection, _saturdayWindowService, _operatingDate());
        await EnsureSpaceAsync(selected, progress, cancellationToken);
        foreach (var item in selected)
        {
            await EnsureSpaceAsync([item], progress, cancellationToken);
            progress?.Report(new DownloadProgress(item, 0, item.Assets.Single().ExpectedSizeBytes, "Iniciando download"));
            var downloaded = await downloader.DownloadAsync(item, progress, cancellationToken);
            progress?.Report(new DownloadProgress(downloaded, downloaded.Assets.Single().ExpectedSizeBytes ?? 0, downloaded.Assets.Single().ExpectedSizeBytes, "Validado e disponível offline"));
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

    private IReadOnlyList<ContentItem> SelectWithoutWindowService(IReadOnlyList<ContentItem> candidates, DownloadSelection selection)
    {
        if (selection.DownloadsQuarterly) return candidates.OrderBy(item => item.ScheduledDate).ToArray();
        var current = _operatingDate().AddDays(-((int)_operatingDate().DayOfWeek + 1) % 7);
        var dates = new[]
        {
            (current.AddDays(-7), selection.PreviousSaturday),
            (current, selection.CurrentSaturday),
            (current.AddDays(7), selection.NextSaturday)
        }.Where(item => item.Item2).Select(item => item.Item1).ToHashSet();
        return candidates.Where(item => dates.Contains(item.ScheduledDate)).OrderBy(item => item.ScheduledDate).ToArray();
    }
}
