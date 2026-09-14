using Sinalo.Domain;

namespace Sinalo.Application.Configuration;

/// <summary>
/// Programas de vídeo que o Sinalo reconhece e seus endereços oficiais padrão.
/// Os endereços podem ser alterados pelo operador quando uma fonte mudar.
/// </summary>
public static class OfficialContentPrograms
{
    public static readonly SourceConfiguration Missions = new(
        ContentSource.Missions,
        "Informativo das Missões",
        "https://www.daniellocutor.com.br/",
        AvailabilityPolicy.MonthlyFull);

    public static readonly SourceConfiguration ProvaiEVede = new(
        ContentSource.ProvaiEVede,
        "Provai e Vede",
        "https://www.adventistas.org/pt/mordomiacrista/projeto/provai-e-vede/",
        AvailabilityPolicy.QuarterlyFull);

    public static readonly SourceConfiguration Health = new(
        ContentSource.Health,
        "Minuto de Saúde",
        "https://downloads.adventistas.org/pt/",
        AvailabilityPolicy.QuarterlyFull);

    public static IReadOnlyList<SourceConfiguration> All { get; } = [Missions, ProvaiEVede, Health];

    public static SourceConfiguration Get(ContentSource source) => source switch
    {
        ContentSource.Missions => Missions,
        ContentSource.ProvaiEVede => ProvaiEVede,
        ContentSource.Health => Health,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Programa de vídeo desconhecido.")
    };
}
