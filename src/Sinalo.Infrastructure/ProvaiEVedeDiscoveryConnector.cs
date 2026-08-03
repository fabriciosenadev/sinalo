using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class ProvaiEVedeDiscoveryConnector(HttpClient httpClient) : IContentDiscoveryConnector
{
    private readonly OfficialFileDiscoveryConnector _inner = new(ContentSource.ProvaiEVede, httpClient);
    public ContentSource Source => ContentSource.ProvaiEVede;

    public Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.Source != ContentSource.ProvaiEVede) throw new InvalidOperationException("Este conector atende somente Provai e Vede.");
        return _inner.DiscoverAsync(configuration, cancellationToken);
    }
}
