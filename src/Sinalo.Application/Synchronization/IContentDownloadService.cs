using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public interface IContentDownloadService
{
    Task<ContentItem> DownloadAsync(ContentItem item, CancellationToken cancellationToken = default);
}
