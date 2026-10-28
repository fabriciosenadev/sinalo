using Sinalo.Application.Catalog;
using Sinalo.Application.Library;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed class LinkedVideoExecutionService(ILinkedVideoService downloader, IContentCatalog catalog,
    IMediaCategoryService? categories = null, IChannelVideoService? channelVideos = null,
    IHealthYouTubeConfigurationService? configuration = null)
{
    public async Task<ContentItem?> ExecuteAsync(LinkedVideoDownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken token = default)
    {
        if (request.ChannelApproval is { } approval)
        {
            if (categories is null || channelVideos is null || configuration is null) throw new InvalidOperationException("A fonte secundária não está disponível.");
            var current = await configuration.LoadAsync(token).ConfigureAwait(false);
            if (!current.Enabled || current != approval.Configuration) throw new ChannelVideoApprovalRequiredException("A configuração do canal mudou. Consulte e selecione os vídeos novamente.");
            if (request.Destination != ContentSource.Health || request.Video.Id != approval.Candidate.Video.Id || approval.Candidate.ChannelId != current.ChannelId)
                throw new ChannelVideoApprovalRequiredException("O pedido não corresponde ao vídeo aprovado.");
            var existing = await categories.FindLinkedAnywhereAsync(request.Video.Id, token).ConfigureAwait(false);
            if (existing?.CanPlay == true && File.Exists(existing.LocalPath)) return null;
            var fresh = await channelVideos.InspectAsync(request.Video.Id, current, token).ConfigureAwait(false);
            if (fresh is null || fresh.ChannelId != approval.Candidate.ChannelId) throw new ChannelVideoApprovalRequiredException("O vídeo aprovado não está disponível. Consulte a lista novamente.");
            var format = fresh.Video.Formats.FirstOrDefault(x => x.Selector == request.Format.Selector && x.Height == request.Format.Height);
            if (format is null || format.Height > current.MaximumHeight || fresh.Video.Title != approval.Candidate.Video.Title || fresh.Video.PublishedDate != approval.Candidate.Video.PublishedDate)
                throw new ChannelVideoApprovalRequiredException("O vídeo ou a qualidade mudou. Consulte e aprove novamente.");
            if (request.Format.EstimatedBytes is > 0 && format.EstimatedBytes is { } size && size > request.Format.EstimatedBytes * 1.25 + 32 * 1024 * 1024)
                throw new ChannelVideoApprovalRequiredException("O tamanho estimado aumentou. Confira e aprove novamente.");
            request = request with
            {
                Video = fresh.Video, Format = format, LibraryStorage = true, CustomUsageDate = existing?.UsageDate,
                CategoryId = existing?.CategoryId ?? MediaCategory.ForSource(ContentSource.Health),
                ExistingMediaId = existing?.Id,
                ExistingManagedPath = existing?.StorageMode == MediaStorageMode.Managed ? existing.LocalPath : null
            };
        }
        if (categories is not null)
        {
            await categories.ValidateDestinationAsync(request.EffectiveCategoryId, token);
            if (await categories.FindLinkedAsync(request.Video.Id, request.EffectiveCategoryId, token) is { CanPlay: true }) return null;
        }
        else
        {
            var existing = await catalog.FindByIdAsync(request.ItemId, token);
            if (existing?.IsReadyOffline == true && existing.LocalPath is { } path && File.Exists(path)) return null;
        }
        var result = await downloader.DownloadAsync(request, progress, token).ConfigureAwait(false);
        if ((request.IsCustomDestination || request.LibraryStorage) && categories is not null) await categories.PublishLinkedAsync(request, result, token);
        else await catalog.UpsertAsync([result], token);
        return result;
    }
}
