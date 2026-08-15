using Sinalo.Domain;

namespace Sinalo.Application.Configuration;

public sealed record SourceConfiguration(
    ContentSource Source,
    string DisplayName,
    string PageUrl,
    AvailabilityPolicy Policy,
    DownloadSelection? DownloadSelection = null)
{
    // Instalações anteriores persistem somente Policy. Enquanto não houver a nova seleção,
    // a equivalência preserva exatamente o comportamento que o operador já escolheu.
    public DownloadSelection ResolvedDownloadSelection => DownloadSelection ?? DownloadSelection.FromLegacyPolicy(Policy);
}
