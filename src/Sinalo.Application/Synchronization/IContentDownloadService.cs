using Sinalo.Domain;
namespace Sinalo.Application.Synchronization;

public sealed record DownloadProgress(ContentItem Item, long BytesReceived, long? TotalBytes, string Stage)
{
    public double? Percentage => TotalBytes is > 0 ? Math.Round((double)BytesReceived / TotalBytes.Value * 100, 1) : null;
}

public interface IContentDownloadService
{
    Task<ContentItem> DownloadAsync(ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
