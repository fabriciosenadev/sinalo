using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Configuration;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly List<CatalogCard> _allCatalogItems;

    public HomeViewModel(
        ISaturdayWindowService saturdayWindowService,
        ISinaloPathService pathService,
        IReadOnlyList<SourceConfiguration> configurations,
        IReadOnlyList<ContentItem>? catalogItems = null, IReadOnlyList<PlaybackScreenOption>? playbackScreens = null, int? selectedPlaybackScreenNumber = null)
    {
        var window = saturdayWindowService.GetWindow(DateOnly.FromDateTime(DateTime.Today));
        PreviousSaturday = FormatDate(window.Previous);
        CurrentSaturday = FormatDate(window.Current);
        NextSaturday = FormatDate(window.Next);
        ContentPath = pathService.GetPaths().ContentPath;
        Sources = configurations.Select(item => new SourceCard(
            item.Source,
            item.DisplayName,
            item.Policy == AvailabilityPolicy.QuarterlyFull ? "Trimestre completo" : "Janela semanal ou mês completo",
            string.IsNullOrWhiteSpace(item.PageUrl) ? "Configuração da fonte pendente" : "Fonte configurada"))
            .ToArray();
        _allCatalogItems = (catalogItems ?? [])
            .OrderBy(item => item.ScheduledDate)
            .Select(MapItem)
            .ToList();
        PlaybackScreens = playbackScreens ?? [new PlaybackScreenOption("Abrir normalmente", null)];
        SelectedPlaybackScreen = PlaybackScreens.FirstOrDefault(screen => screen.ScreenNumber == selectedPlaybackScreenNumber) ?? PlaybackScreens.FirstOrDefault();
        ApplyFilters();
        OperationMessage = _allCatalogItems.Count == 0
            ? "Nenhum conteúdo no catálogo local. Atualize uma fonte para começar."
            : $"{_allCatalogItems.Count} conteúdo(s) no catálogo local.";
    }

    [ObservableProperty] private string previousSaturday = string.Empty;
    [ObservableProperty] private string currentSaturday = string.Empty;
    [ObservableProperty] private string nextSaturday = string.Empty;
    [ObservableProperty] private string contentPath = string.Empty;
    [ObservableProperty] private string selectedSource = "Todos";
    [ObservableProperty] private string selectedAvailability = "Todos";
    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private CatalogCard? selectedCatalogItem;
    [ObservableProperty] private string operationMessage = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double syncProgressPercent;
    [ObservableProperty] private string syncProgressLabel = string.Empty;
    [ObservableProperty] private PlaybackScreenOption? selectedPlaybackScreen;

    public IReadOnlyList<SourceCard> Sources { get; }
    public ObservableCollection<CatalogCard> CatalogItems { get; } = [];
    public ObservableCollection<ScheduleCard> ScheduleItems { get; } = [];
    public IReadOnlyList<PlaybackScreenOption> PlaybackScreens { get; }

    public string SelectedItemTitle => SelectedCatalogItem?.Title ?? "Selecione um vídeo";
    public string SelectedItemDetails => SelectedCatalogItem is null
        ? "Escolha um conteúdo para ver detalhes e adicioná-lo à programação."
        : $"{SelectedCatalogItem.SourceName} • {SelectedCatalogItem.ScheduledDate} • {SelectedCatalogItem.Status}";
    public string SelectedItemPath => SelectedCatalogItem?.LocalPath ?? "Arquivo local ainda não disponível.";
    public bool HasSelectedItem => SelectedCatalogItem is not null;

    partial void OnSelectedSourceChanged(string value) => ApplyFilters();
    partial void OnSelectedAvailabilityChanged(string value) => ApplyFilters();
    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedCatalogItemChanged(CatalogCard? value)
    {
        OnPropertyChanged(nameof(SelectedItemTitle));
        OnPropertyChanged(nameof(SelectedItemDetails));
        OnPropertyChanged(nameof(SelectedItemPath));
        OnPropertyChanged(nameof(HasSelectedItem));
    }

    public void AddSelectedToSchedule()
    {
        if (SelectedCatalogItem is null || ScheduleItems.Any(item => item.Id == SelectedCatalogItem.Id)) return;
        ScheduleItems.Add(new ScheduleCard(SelectedCatalogItem.Id, SelectedCatalogItem.Title, SelectedCatalogItem.SourceName, SelectedCatalogItem.Status));
        OperationMessage = $"{SelectedCatalogItem.Title} adicionado à programação.";
    }

    public void RemoveFromSchedule(ScheduleCard? item)
    {
        if (item is null) return;
        ScheduleItems.Remove(item);
        OperationMessage = $"{item.Title} removido da programação.";
    }

    public void MoveScheduleItem(ScheduleCard? item, int direction)
    {
        if (item is null) return;
        var currentIndex = ScheduleItems.IndexOf(item);
        var targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= ScheduleItems.Count) return;
        ScheduleItems.Move(currentIndex, targetIndex);
    }

    public void ReportDownloadProgress(Sinalo.Application.Synchronization.DownloadProgress progress)
    {
        SyncProgressPercent = progress.Percentage ?? 0;
        SyncProgressLabel = progress.Percentage is { } percentage
            ? $"{progress.Item.Title}: {progress.Stage} ({percentage:0.0}%)"
            : $"{progress.Item.Title}: {progress.Stage}";
        OperationMessage = SyncProgressLabel;
        if (progress.Item.SyncState == SyncState.Ready) MarkItemAsReady(progress.Item);
    }

    public void MarkItemAsReady(ContentItem item)
    {
        var index = _allCatalogItems.FindIndex(card => card.Id == item.Id);
        var card = MapItem(item);
        if (index >= 0) _allCatalogItems[index] = card;
        else _allCatalogItems.Add(card);
        ApplyFilters();
        SelectedCatalogItem = card;
    }

    public void MarkItemAsPlayed(ContentItem item)
    {
        var index = _allCatalogItems.FindIndex(card => card.Id == item.Id);
        var card = MapItem(item);
        if (index >= 0) _allCatalogItems[index] = card;
        else _allCatalogItems.Add(card);
        ApplyFilters();
        SelectedCatalogItem = card;
    }

    private void ApplyFilters()
    {
        var filtered = _allCatalogItems.Where(item =>
            (SelectedSource == "Todos" || item.SourceName == SelectedSource) &&
            (SelectedAvailability == "Todos" || item.Status == SelectedAvailability) &&
            (string.IsNullOrWhiteSpace(SearchQuery) || item.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));

        CatalogItems.Clear();
        foreach (var item in filtered) CatalogItems.Add(item);
        if (SelectedCatalogItem is not null && !CatalogItems.Contains(SelectedCatalogItem)) SelectedCatalogItem = null;
    }

    private static CatalogCard MapItem(ContentItem item) => new(
        item.Id,
        item.Title,
        GetSourceName(item.Source),
        item.ScheduledDate.ToString("dd/MM/yyyy"),
        GetCatalogStatus(item),
        item.LocalPath,
        item.IsReadyOffline ? "▶" : item.SyncState == SyncState.OnlineOnly ? "◌" : "↓",
        item.PlayCount == 0 ? string.Empty : $"Reproduzido {item.PlayCount}×");

    private static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy");
    private static string GetSourceName(ContentSource source) => source switch
    {
        ContentSource.Missions => "Informativo das Missões",
        ContentSource.ProvaiEVede => "Provai e Vede",
        ContentSource.Health => "Minuto de Saúde",
        _ => source.ToString()
    };
    private static string GetCatalogStatus(ContentItem item) =>
        item.Assets.Count == 0 ? "Página trimestral identificada" :
        item.SyncState == SyncState.OnlineOnly ? "Somente online" :
        item.SyncState == SyncState.Ready ? "Pronto offline" :
        item.SyncState == SyncState.Failed ? "Falhou" : "Disponível para sincronizar";
}

public sealed record SourceCard(ContentSource Source, string Name, string SyncPolicy, string Status);
public sealed record CatalogCard(string Id, string Title, string SourceName, string ScheduledDate, string Status, string? LocalPath, string ThumbnailGlyph, string PlaybackLabel = "")
{
    // Compatibilidade com consumidores que já exibiam a coluna "Source" da lista anterior.
    public string Source => SourceName;
}
public sealed record ScheduleCard(string Id, string Title, string SourceName, string Status);
public sealed record PlaybackScreenOption(string Label, int? ScreenNumber);
