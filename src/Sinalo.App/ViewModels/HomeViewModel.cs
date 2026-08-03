using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    public HomeViewModel(ISaturdayWindowService saturdayWindowService, ISinaloPathService pathService)
    {
        var window = saturdayWindowService.GetWindow(DateOnly.FromDateTime(DateTime.Today));

        PreviousSaturday = FormatDate(window.Previous);
        CurrentSaturday = FormatDate(window.Current);
        NextSaturday = FormatDate(window.Next);
        ContentPath = pathService.GetPaths().ContentPath;
    }

    [ObservableProperty]
    private string previousSaturday = string.Empty;

    [ObservableProperty]
    private string currentSaturday = string.Empty;

    [ObservableProperty]
    private string nextSaturday = string.Empty;

    [ObservableProperty]
    private string contentPath = string.Empty;

    public IReadOnlyList<SourceCard> Sources { get; } =
    [
        new(ContentSource.Missions, "Informativo das Missões", "Janela semanal ou mês completo"),
        new(ContentSource.ProvaiEVede, "Provai e Vede", "Trimestre completo"),
        new(ContentSource.Health, "Minuto de Saúde", "Janela semanal ou mês completo")
    ];

    private static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy");
}

public sealed record SourceCard(ContentSource Source, string Name, string SyncPolicy);
