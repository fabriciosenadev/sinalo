using Sinalo.Domain;

namespace Sinalo.Application.Catalog;

public interface IContentCatalog
{
    Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default);
}
