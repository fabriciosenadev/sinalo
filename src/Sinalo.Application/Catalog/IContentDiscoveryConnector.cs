using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Application.Catalog;

public interface IContentDiscoveryConnector
{
    ContentSource Source { get; }

    Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default);
}
