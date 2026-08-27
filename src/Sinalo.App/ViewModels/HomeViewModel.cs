using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Configuration;
using Sinalo.Application.Services;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
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
            GetSyncPolicyDescription(item),
            string.IsNullOrWhiteSpace(item.PageUrl) ? "Configuração da fonte pendente" : "Fonte configurada"))
            .ToArray();
        _allCatalogItems = (catalogItems ?? [])
            .Where(item => item.IsReadyOffline)
            .OrderBy(item => item.ScheduledDate)
            .Select(MapItem)
            .ToList();
        PlaybackScreens = playbackScreens ?? [new PlaybackScreenOption("Tela 1 · Principal", 1, true)];
        SelectedPlaybackScreen = PlaybackScreens.FirstOrDefault(screen => screen.ScreenNumber == selectedPlaybackScreenNumber) ?? PlaybackScreens.FirstOrDefault();
        ApplyFilters();
        OperationMessage = _allCatalogItems.Count == 0
            ? "Nenhum vídeo offline disponível. Escolha uma fonte e use Buscar e baixar."
            : $"{_allCatalogItems.Count} vídeo(s) offline no catálogo local.";
    }

    private static string GetSyncPolicyDescription(SourceConfiguration configuration)
    {
        if (configuration.Source == ContentSource.Missions && configuration.DownloadSelection is null) return "Janela semanal ou mês completo";
        if (configuration.ResolvedDownloadSelection.DownloadsQuarterly) return "Trimestre completo";
        var selected = new[]
        {
            ("Sáb. anterior", configuration.ResolvedDownloadSelection.PreviousSaturday),
            ("Sáb. atual", configuration.ResolvedDownloadSelection.CurrentSaturday),
            ("Próximo sáb.", configuration.ResolvedDownloadSelection.NextSaturday)
        }.Where(item => item.Item2).Select(item => item.Item1);
        return string.Join(", ", selected);
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
    [ObservableProperty] private bool isQueueActive;
    [ObservableProperty] private bool isUpdateAvailable;
    [ObservableProperty] private bool isUpdateDownloading;
    [ObservableProperty] private bool isUpdateReady;
    [ObservableProperty] private string updateMessage = string.Empty;
    [ObservableProperty] private double updateProgressPercent;

    public IReadOnlyList<SourceCard> Sources { get; }
    public ObservableCollection<CatalogCard> CatalogItems { get; } = [];
    public ObservableCollection<ScheduleCard> ScheduleItems { get; } = [];
    public ObservableCollection<SynchronizationQueueCard> SynchronizationQueueItems { get; } = [];
    public IReadOnlyList<PlaybackScreenOption> PlaybackScreens { get; }

    public string SelectedItemTitle => SelectedCatalogItem?.Title ?? "Selecione um vídeo";
    public string SelectedItemDetails => SelectedCatalogItem is null
        ? "Escolha um conteúdo para ver detalhes e adicioná-lo à programação."
        : $"{SelectedCatalogItem.SourceName} • {SelectedCatalogItem.ScheduledDate} • {SelectedCatalogItem.Status}";
    public string SelectedItemPath => SelectedCatalogItem?.LocalPath ?? "Arquivo local ainda não disponível.";
    public bool HasSelectedItem => SelectedCatalogItem is not null;

    public string SelectedSourceActionLabel => SelectedSource == "Todos" ? "Selecione uma fonte" : SelectedSource;
    public string UpdateAndSynchronizeSelectedSourceLabel => $"Buscar e baixar {SelectedSourceActionLabel}";
    public bool CanOperateSelectedSource => Sources.SingleOrDefault(source => source.Name == SelectedSource)?.Source is ContentSource.Missions or ContentSource.ProvaiEVede or ContentSource.Health;
    public bool CanQueueSelectedSource => CanOperateSelectedSource && !SynchronizationQueueItems.Any(item => item.SourceName == SelectedSource && item.IsPending);
    public bool IsHealthSelected => Sources.SingleOrDefault(source => source.Name == SelectedSource)?.Source == ContentSource.Health;

    partial void OnSelectedSourceChanged(string value)
    {
        ApplyFilters();
        OnPropertyChanged(nameof(SelectedSourceActionLabel));
        OnPropertyChanged(nameof(UpdateAndSynchronizeSelectedSourceLabel));
        OnPropertyChanged(nameof(CanOperateSelectedSource));
        OnPropertyChanged(nameof(CanQueueSelectedSource));
        OnPropertyChanged(nameof(IsHealthSelected));
    }
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

    public void UpdateSynchronizationQueue(SynchronizationQueueSnapshot snapshot)
    {
        IsQueueActive = snapshot.IsProcessing;
        SynchronizationQueueItems.Clear();
        foreach (var entry in snapshot.Entries)
        {
            var pending = entry.State is SynchronizationQueueState.Waiting or SynchronizationQueueState.Running;
            SynchronizationQueueItems.Add(new SynchronizationQueueCard(entry.SourceName, GetQueueStateLabel(entry.State), entry.Message, pending));
        }

        var active = snapshot.Entries.FirstOrDefault(entry => entry.State == SynchronizationQueueState.Running);
        if (active is not null)
        {
            IsBusy = true;
            SyncProgressPercent = active.Percentage ?? 0;
            SyncProgressLabel = active.Message;
            OperationMessage = $"{active.SourceName}: {active.Message}";
        }
        else
        {
            IsBusy = false;
            var last = snapshot.Entries.LastOrDefault();
            if (last is not null) OperationMessage = $"{last.SourceName}: {last.Message}";
        }
        OnPropertyChanged(nameof(CanQueueSelectedSource));
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

    public void RemoveCatalogItem(string id)
    {
        _allCatalogItems.RemoveAll(item => item.Id == id);
        for (var index = ScheduleItems.Count - 1; index >= 0; index--)
            if (ScheduleItems[index].Id == id) ScheduleItems.RemoveAt(index);
        ApplyFilters();
    }

    public void ReportUpdateAvailable(Version version)
    {
        IsUpdateAvailable = true;
        IsUpdateDownloading = true;
        UpdateMessage = $"Nova versão {version} encontrada. Baixando atualização...";
    }

    public void ReportUpdateProgress(double percentage)
    {
        if (IsUpdateReady) return;
        UpdateProgressPercent = percentage;
        UpdateMessage = $"Nova versão disponível. Baixando {percentage:0}%...";
    }

    public void ReportUpdateReady(Version version)
    {
        IsUpdateDownloading = false;
        IsUpdateReady = true;
        UpdateProgressPercent = 100;
        UpdateMessage = $"Versão {version} pronta para instalar.";
    }

    public void ReportUpdateFailure()
    {
        IsUpdateDownloading = false;
        UpdateMessage = "Há uma nova versão, mas o download não foi concluído.";
    }

    private void ApplyFilters()
    {
        var filtered = _allCatalogItems.Where(item =>
            (SelectedSource == "Todos" || item.SourceName == SelectedSource) &&
            (SelectedAvailability == "Todos" || item.Status == SelectedAvailability) &&
            (string.IsNullOrWhiteSpace(SearchQuery) ||
             item.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
             item.ScheduledDate.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));

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

    private static string GetQueueStateLabel(SynchronizationQueueState state) => state switch
    {
        SynchronizationQueueState.Waiting => "Na fila",
        SynchronizationQueueState.Running => "Baixando",
        SynchronizationQueueState.Completed => "Concluída",
        SynchronizationQueueState.Failed => "Falhou",
        SynchronizationQueueState.Cancelled => "Cancelada",
        _ => state.ToString()
    };
}

public sealed record SourceCard(ContentSource Source, string Name, string SyncPolicy, string Status);
public sealed record CatalogCard(string Id, string Title, string SourceName, string ScheduledDate, string Status, string? LocalPath, string ThumbnailGlyph, string PlaybackLabel = "")
{
    // Compatibilidade com consumidores que já exibiam a coluna "Source" da lista anterior.
    public string Source => SourceName;
}
public sealed record ScheduleCard(string Id, string Title, string SourceName, string Status);
public sealed record PlaybackScreenOption(string Label, int ScreenNumber, bool IsPrimary = false);
public sealed record SynchronizationQueueCard(string SourceName, string State, string Details, bool IsPending);
