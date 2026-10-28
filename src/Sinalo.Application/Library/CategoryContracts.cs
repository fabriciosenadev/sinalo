using Sinalo.Domain;

namespace Sinalo.Application.Library;

public sealed record CategoryImpact(int ManagedCopies, int References, int Pinned, long Bytes, IReadOnlyList<string> MediaIds);
public interface IMediaCategoryService
{
    event Action? Changed;
    event Action<IReadOnlyList<string>>? MediaRemoved;
    Task<IReadOnlyList<MediaCategory>> ListAsync(CancellationToken token = default);
    Task<MediaCategory> CreateAsync(string name, CancellationToken token = default);
    Task RenameAsync(MediaCategory category, string name, CancellationToken token = default);
    Task ReorderAsync(string id, int direction, CancellationToken token = default);
    Task AssignAsync(IReadOnlyList<string> mediaIds, string categoryId, CancellationToken token = default);
    Task ValidateDestinationAsync(string categoryId, CancellationToken token = default);
    Task<CategoryImpact> InspectAsync(string categoryId, CancellationToken token = default);
    Task RemoveAsync(MediaCategory category, string? transferTo, CancellationToken token = default, IReadOnlyList<string>? expectedMediaIds = null);
    Task<LibraryMedia?> FindLinkedAsync(string videoId, string categoryId, CancellationToken token = default);
    Task<LibraryMedia?> FindLinkedAnywhereAsync(string videoId, CancellationToken token = default)
        => FindLinkedAsync(videoId, MediaCategory.ForSource(ContentSource.Health), token);
    Task PublishLinkedAsync(Sinalo.Application.Synchronization.LinkedVideoDownloadRequest request, ContentItem downloaded, CancellationToken token = default);
}
