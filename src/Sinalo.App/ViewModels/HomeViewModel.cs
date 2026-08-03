using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    public HomeViewModel(ISaturdayWindowService saturdayWindowService, ISinaloPathService pathService, IReadOnlyList<SourceConfiguration> configurations)
    {
        var window = saturdayWindowService.GetWindow(DateOnly.FromDateTime(DateTime.Today));

        PreviousSaturday = FormatDate(window.Previous);
        CurrentSaturday = FormatDate(window.Current);
        NextSaturday = FormatDate(window.Next);
        ContentPath = pathService.GetPaths().ContentPath;
        Sources = configurations.Select(item => new SourceCard(item.Source, item.DisplayName, item.Policy == AvailabilityPolicy.QuarterlyFull ? "Trimestre completo" : "Janela semanal ou mês completo", string.IsNullOrWhiteSpace(item.PageUrl) ? "Configuração da fonte pendente" : "Fonte configurada")).ToArray();
    }

    [ObservableProperty]
    private string previousSaturday = string.Empty;

    [ObservableProperty]
    private string currentSaturday = string.Empty;

    [ObservableProperty]
    private string nextSaturday = string.Empty;

    [ObservableProperty]
    private string contentPath = string.Empty;

    public IReadOnlyList<SourceCard> Sources { get; }

    private static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy");
}

public sealed record SourceCard(ContentSource Source, string Name, string SyncPolicy, string Status);
