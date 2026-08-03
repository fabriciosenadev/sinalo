using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Application.Catalog;

public sealed class ContentDiscoveryService(
    IEnumerable<IContentDiscoveryConnector> connectors,
    IContentCatalog catalog)
{
    private readonly IReadOnlyList<IContentDiscoveryConnector> _connectors = connectors.ToArray();
    private readonly IContentCatalog _catalog = catalog;

    public async Task<IReadOnlyList<ContentItem>> RefreshAsync(
        SourceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.PageUrl))
        {
            return [];
        }

        var connector = _connectors.SingleOrDefault(item => item.Source == configuration.Source)
            ?? throw new InvalidOperationException($"Nenhum conector foi configurado para a fonte {configuration.Source}.");
        var items = await connector.DiscoverAsync(configuration, cancellationToken);

        if (items.Any(item => item.Source != configuration.Source))
        {
            throw new InvalidOperationException("O conector retornou conteúdo de uma fonte diferente da configurada.");
        }

        await _catalog.UpsertAsync(items, cancellationToken);
        return items;
    }
}
