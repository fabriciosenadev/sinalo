using Sinalo.Domain;

namespace Sinalo.Application.Catalog;

public interface IContentCatalog
{
    Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default);

    Task<ContentItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ContentItem?>(null);

    Task RecordPlaybackAsync(string id, DateTimeOffset playedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task RelocateLocalPathsAsync(string previousContentPath, string newContentPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
