using CommunityToolkit.Mvvm.ComponentModel;
namespace Sinalo.App.ViewModels;

/// <summary>Estado de interface. Não altera política de downloads ou persistência.</summary>
public sealed partial class HomeViewModel
{
    [ObservableProperty] private bool detailsOpen;
    [ObservableProperty] private bool isPresentationOpen;
    [ObservableProperty] private string synchronizationMessage = "Nenhum download nesta sessão.";
    [ObservableProperty] private bool isInstallingUpdate;
    [ObservableProperty] private bool isCompactWorkspace;
    [ObservableProperty] private bool isSynchronizationIndeterminate;
    public string DownloadsLabel => $"Downloads{(SynchronizationQueueItems.Count(item => item.IsPending) is > 0 and var count ? $" ({count})" : "")}";
    public bool IsScheduleEmpty => ScheduleItems.Count == 0;
    private void RefreshSchedulePositions()
    {
        for (var i = 0; i < ScheduleItems.Count; i++)
        {
            ScheduleItems[i].Position = i + 1;
            ScheduleItems[i].CanMoveUp = i > 0;
            ScheduleItems[i].CanMoveDown = i < ScheduleItems.Count - 1;
        }
        OnPropertyChanged(nameof(IsScheduleEmpty));
    }
    public void RestoreWorkspaceState(HomeViewModel previous)
    {
        IsTimerWorkspace = previous.IsTimerWorkspace;
        IsRaffleWorkspace = previous.IsRaffleWorkspace;
        IsWorshipTimerWorkspace = previous.IsWorshipTimerWorkspace;
        SelectedCatalogItem = CatalogItems.FirstOrDefault(item => item.Id == previous.SelectedCatalogItem?.Id);
        DetailsOpen = previous.DetailsOpen && HasSelectedItem;
        ScheduleItems.Clear();
        foreach (var item in previous.ScheduleItems) ScheduleItems.Add(new(item.Id, item.Title, item.SourceName, item.Status));
        SynchronizationQueueItems.Clear();
        foreach (var item in previous.SynchronizationQueueItems) SynchronizationQueueItems.Add(item);
        IsQueueActive = previous.IsQueueActive;
        IsBusy = previous.IsBusy;
        SynchronizationMessage = previous.SynchronizationMessage;
        SyncProgressPercent = previous.SyncProgressPercent;
        IsSynchronizationIndeterminate = previous.IsSynchronizationIndeterminate;
        IsPresentationOpen = previous.IsPresentationOpen;
        IsUpdateAvailable = previous.IsUpdateAvailable;
        IsUpdateDownloading = previous.IsUpdateDownloading;
        IsUpdateReady = previous.IsUpdateReady;
        IsInstallingUpdate = previous.IsInstallingUpdate;
        UpdateMessage = previous.UpdateMessage;
        UpdateProgressPercent = previous.UpdateProgressPercent;
        OnPropertyChanged(nameof(DownloadsLabel));
        OnPropertyChanged(nameof(HasSynchronizationQueueItems));
        if (!IsLibraryWorkspace) OperationMessage = previous.OperationMessage;
        RefreshWorkspace();
    }
    public bool ShowProgramCalendar => IsLibraryWorkspace && !IsCompactWorkspace;
    partial void OnIsCompactWorkspaceChanged(bool value) => OnPropertyChanged(nameof(ShowProgramCalendar));
    public string WorkspaceTitle => IsTimerWorkspace ? "Cronômetro" : IsWorshipTimerWorkspace ? "Cronômetro de Culto" : IsRaffleWorkspace ? "Sorteio" : IsLinkedVideoWorkspace ? "Adicionar vídeo por link" : SelectedSource == "Todos" ? "Programas de vídeo" : SelectedSource;
    public string CatalogSummary => $"{CatalogItems.Count} vídeo(s) pronto(s) para reproduzir";
    public bool IsCatalogEmpty => CatalogItems.Count == 0;
    public string CatalogEmptyMessage => !string.IsNullOrWhiteSpace(SearchQuery) ? "Nenhum vídeo corresponde à pesquisa. Limpe a pesquisa para ver os demais." : "Nenhum vídeo local neste programa. Busque os arquivos publicados ou adicione um vídeo por link.";
    public string SelectedSourcePolicy => Sources.FirstOrDefault(source => source.Name == SelectedSource)?.SyncPolicy ?? "Escolha um programa para preparar seus vídeos.";
    public bool ShowCatalogDetails => IsLibraryWorkspace && HasSelectedItem && DetailsOpen;
    public bool HasLinkedVideo => !string.IsNullOrEmpty(LinkedVideoTitle);
    public string LinkedVideoSummary => HasLinkedVideo ? $"{SelectedLinkedVideoDestination?.Label ?? "Escolha o programa"} · {LinkedVideoDateText} · {SelectedLinkedVideoFormat?.Label ?? "Escolha a qualidade"}" : "Consulte o vídeo antes de preparar o download.";
    public string SelectedPlaybackLabel => SelectedPlaybackScreen is null ? "Nenhuma tela disponível" : $"Reproduzir em {SelectedPlaybackScreen.Label}";
    partial void OnSelectedPlaybackScreenChanged(PlaybackScreenOption? value) => OnPropertyChanged(nameof(SelectedPlaybackLabel));
    partial void OnDetailsOpenChanged(bool value) => OnPropertyChanged(nameof(ShowCatalogDetails));
    private void RefreshWorkspace()
    {
        foreach (var source in Sources) source.IsSelected = IsLibraryWorkspace && source.Name == SelectedSource;
        OnPropertyChanged(nameof(IsLibraryWorkspace));
        OnPropertyChanged(nameof(ShowProgramCalendar));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(SelectedSourcePolicy));
        OnPropertyChanged(nameof(ShowCatalogDetails));
    }
    public void SelectProgram(string name)
    {
        IsTimerWorkspace = IsWorshipTimerWorkspace = IsRaffleWorkspace = IsLinkedVideoWorkspace = false;
        SelectedSource = name;
        OperationMessage = CatalogSummary;
        RefreshWorkspace();
    }
}
