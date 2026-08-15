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
        var discoveredItems = await connector.DiscoverAsync(configuration, cancellationToken);

        if (discoveredItems.Any(item => item.Source != configuration.Source))
        {
            throw new InvalidOperationException("O conector retornou conteúdo de uma fonte diferente da configurada.");
        }

        var existingById = (await _catalog.ListBySourceAsync(configuration.Source, cancellationToken))
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var items = discoveredItems
            .Select(item => PreserveAvailableLocalFile(item, existingById))
            .ToArray();
        await _catalog.UpsertAsync(items, cancellationToken);
        return items;
    }

    private static ContentItem PreserveAvailableLocalFile(ContentItem discovered, IReadOnlyDictionary<string, ContentItem> existingById)
    {
        if (!existingById.TryGetValue(discovered.Id, out var existing) ||
            existing.SyncState != SyncState.Ready ||
            string.IsNullOrWhiteSpace(existing.LocalPath) ||
            !File.Exists(existing.LocalPath))
        {
            return discovered;
        }

        return discovered with
        {
            SyncState = SyncState.Ready,
            LocalPath = existing.LocalPath,
            IsPinned = existing.IsPinned,
            PlayCount = existing.PlayCount,
            FirstPlayedAtUtc = existing.FirstPlayedAtUtc,
            LastPlayedAtUtc = existing.LastPlayedAtUtc
        };
    }
}
