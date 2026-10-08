using System.Collections.ObjectModel;
using System.Globalization;
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
        IReadOnlyList<ContentItem>? catalogItems = null, IReadOnlyList<PlaybackScreenOption>? playbackScreens = null, int? selectedPlaybackScreenNumber = null, TimerViewModel? timer = null, RaffleViewModel? raffle = null, WorshipTimerViewModel? worshipTimer = null)
    {
        var window = saturdayWindowService.GetWindow(DateOnly.FromDateTime(DateTime.Today));
        PreviousSaturday = FormatDate(window.Previous);
        CurrentSaturday = FormatDate(window.Current);
        NextSaturday = FormatDate(window.Next);
        LinkedVideoDateText = CurrentSaturday;
        ContentPath = pathService.GetPaths().ContentPath;
        Sources = configurations.Select(item => new SourceCard(
            item.Source,
            item.DisplayName,
            GetSyncPolicyDescription(item),
            string.IsNullOrWhiteSpace(item.PageUrl) ? "Configuração do programa pendente" : "Programa configurado"))
            .ToArray();
        _allCatalogItems = (catalogItems ?? [])
            .Where(item => item.IsReadyOffline)
            .OrderBy(item => item.ScheduledDate)
            .Select(MapItem)
            .ToList();
        PlaybackScreens = playbackScreens ?? [new PlaybackScreenOption("Tela 1 · Principal", 1, true)];
        SelectedPlaybackScreen = PlaybackScreens.FirstOrDefault(screen => screen.ScreenNumber == selectedPlaybackScreenNumber) ?? PlaybackScreens.FirstOrDefault();
        Timer = timer ?? new TimerViewModel(new Sinalo.Application.Timer.TimerSession(), new Sinalo.Application.Timer.TimerConfiguration(Sinalo.Application.Timer.TimerDirection.CountUp, TimeSpan.FromMinutes(1), "hh:mm:ss"));
        Raffle = raffle ?? new RaffleViewModel(new Sinalo.Application.Raffle.RaffleSession(), new Sinalo.Application.Raffle.RaffleConfiguration(TimeSpan.FromSeconds(5)));
        WorshipTimer = worshipTimer ?? new WorshipTimerViewModel(new Sinalo.Application.WorshipTimer.WorshipTimerSession(), new WorshipTimerAudioPlayer());
        ScheduleItems.CollectionChanged += (_, _) => RefreshSchedulePositions();
        ApplyFilters();
        OperationMessage = _allCatalogItems.Count == 0
            ? "Nenhum vídeo offline disponível. Escolha um programa e use Buscar e baixar."
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
    [ObservableProperty] private bool isTimerWorkspace;
    [ObservableProperty] private bool isRaffleWorkspace;
    [ObservableProperty] private bool isWorshipTimerWorkspace;
    [ObservableProperty] private bool isLinkedVideoWorkspace;
    [ObservableProperty] private string linkedVideoUrl = string.Empty;
    [ObservableProperty] private string linkedVideoStatus = "Cole o link de um vídeo para consultar as qualidades MP4 disponíveis.";
    [ObservableProperty] private string linkedVideoDateText = string.Empty;
    [ObservableProperty] private LinkedVideoDestinationOption? selectedLinkedVideoDestination;
    [ObservableProperty] private LinkedVideoFormat? selectedLinkedVideoFormat;
    [ObservableProperty] private bool isInspectingLinkedVideo;
    private LinkedVideo? _inspectedLinkedVideo;
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
    public IReadOnlyList<LinkedVideoDestinationOption> LinkedVideoDestinations { get; } =
    [
        new(ContentSource.Missions, "Informativo das Missões"),
        new(ContentSource.ProvaiEVede, "Provai e Vede"),
        new(ContentSource.Health, "Minuto de Saúde")
    ];
    public ObservableCollection<LinkedVideoFormat> LinkedVideoFormats { get; } = [];
    public ObservableCollection<CatalogCard> CatalogItems { get; } = [];
    public ObservableCollection<ScheduleCard> ScheduleItems { get; } = [];
    public ObservableCollection<SynchronizationQueueCard> SynchronizationQueueItems { get; } = [];
    public IReadOnlyList<PlaybackScreenOption> PlaybackScreens { get; }
    public TimerViewModel Timer { get; }
    public RaffleViewModel Raffle { get; }
    public WorshipTimerViewModel WorshipTimer { get; }
    public string ApplicationVersion => $"Versão {typeof(HomeViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}";
    public bool IsLibraryWorkspace => !IsTimerWorkspace && !IsRaffleWorkspace && !IsWorshipTimerWorkspace && !IsLinkedVideoWorkspace;
    public bool CanQueueLinkedVideo => !IsInspectingLinkedVideo && _inspectedLinkedVideo is not null &&
        SelectedLinkedVideoFormat is not null && SelectedLinkedVideoDestination is not null &&
        DateOnly.TryParseExact(LinkedVideoDateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    public bool CanInspectLinkedVideo => !IsInspectingLinkedVideo && !string.IsNullOrWhiteSpace(LinkedVideoUrl);
    public string LinkedVideoTitle => _inspectedLinkedVideo?.Title ?? string.Empty;
    public string LinkedVideoPublishedDate => _inspectedLinkedVideo is null ? string.Empty : $"Publicado em {_inspectedLinkedVideo.PublishedDate:dd/MM/yyyy}";

    public string SelectedItemTitle => SelectedCatalogItem?.Title ?? "Selecione um vídeo";
    public string SelectedItemDetails => SelectedCatalogItem is null
        ? "Escolha um conteúdo para ver detalhes e adicioná-lo à programação."
        : $"{SelectedCatalogItem.SourceName} • {SelectedCatalogItem.ScheduledDate} • {SelectedCatalogItem.Status}";
    public string SelectedItemPath => SelectedCatalogItem?.LocalPath ?? "Arquivo local ainda não disponível.";
    public bool HasSelectedItem => SelectedCatalogItem is not null;
    public bool IsSelectedItemPinned => SelectedCatalogItem?.IsPinned == true;
    public string SelectedItemPinActionLabel => IsSelectedItemPinned ? "Remover fixação" : "Fixar vídeo";

    public string SelectedSourceActionLabel => SelectedSource == "Todos" ? "Selecione um programa" : SelectedSource;
    public string UpdateAndSynchronizeSelectedSourceLabel => "Buscar e baixar";
    public bool CanOperateSelectedSource => Sources.SingleOrDefault(source => source.Name == SelectedSource)?.Source is ContentSource.Missions or ContentSource.ProvaiEVede or ContentSource.Health;
    public bool CanQueueSelectedSource => CanOperateSelectedSource && !SynchronizationQueueItems.Any(item => item.SourceName == SelectedSource && item.IsPending);
    public bool HasSynchronizationQueueItems => SynchronizationQueueItems.Count > 0;
    public bool IsHealthSelected => Sources.SingleOrDefault(source => source.Name == SelectedSource)?.Source == ContentSource.Health;

    partial void OnSelectedSourceChanged(string value)
    {
        IsTimerWorkspace = false;
        IsRaffleWorkspace = false;
        IsWorshipTimerWorkspace = false;
        IsLinkedVideoWorkspace = false;
        ApplyFilters();
        RefreshWorkspace();
        OnPropertyChanged(nameof(SelectedSourceActionLabel));
        OnPropertyChanged(nameof(UpdateAndSynchronizeSelectedSourceLabel));
        OnPropertyChanged(nameof(CanOperateSelectedSource));
        OnPropertyChanged(nameof(CanQueueSelectedSource));
        OnPropertyChanged(nameof(IsHealthSelected));
    }
    partial void OnIsTimerWorkspaceChanged(bool value) => RefreshWorkspace();
    partial void OnIsRaffleWorkspaceChanged(bool value) => RefreshWorkspace();
    partial void OnIsWorshipTimerWorkspaceChanged(bool value) => RefreshWorkspace();
    partial void OnIsLinkedVideoWorkspaceChanged(bool value) => RefreshWorkspace();
    partial void OnLinkedVideoUrlChanged(string value) { ClearInspectedLinkedVideo(); LinkedVideoStatus = "Cole o link de um vídeo para consultar as qualidades MP4 disponíveis."; OnPropertyChanged(nameof(CanInspectLinkedVideo)); }
    partial void OnLinkedVideoDateTextChanged(string value) { OnPropertyChanged(nameof(CanQueueLinkedVideo)); OnPropertyChanged(nameof(LinkedVideoSummary)); }
    partial void OnSelectedLinkedVideoDestinationChanged(LinkedVideoDestinationOption? value) { OnPropertyChanged(nameof(CanQueueLinkedVideo)); OnPropertyChanged(nameof(LinkedVideoSummary)); }
    partial void OnSelectedLinkedVideoFormatChanged(LinkedVideoFormat? value) { OnPropertyChanged(nameof(CanQueueLinkedVideo)); OnPropertyChanged(nameof(LinkedVideoSummary)); }
    partial void OnIsInspectingLinkedVideoChanged(bool value) { OnPropertyChanged(nameof(CanQueueLinkedVideo)); OnPropertyChanged(nameof(CanInspectLinkedVideo)); }
    public void SelectTimerWorkspace() { IsTimerWorkspace = true; IsRaffleWorkspace = false; IsWorshipTimerWorkspace = false; IsLinkedVideoWorkspace = false; OperationMessage = Timer.StateLabel; }
    public void SelectRaffleWorkspace() { IsRaffleWorkspace = true; IsTimerWorkspace = false; IsWorshipTimerWorkspace = false; IsLinkedVideoWorkspace = false; OperationMessage = Raffle.StatusLabel; }
    public void SelectWorshipTimerWorkspace() { IsWorshipTimerWorkspace = true; IsRaffleWorkspace = false; IsTimerWorkspace = false; IsLinkedVideoWorkspace = false; OperationMessage = WorshipTimer.StateLabel; }
    public void SelectLinkedVideoWorkspace() { SelectedSource = "Todos"; IsLinkedVideoWorkspace = true; IsTimerWorkspace = false; IsRaffleWorkspace = false; IsWorshipTimerWorkspace = false; OperationMessage = LinkedVideoStatus; }
    public void RestoreLinkedVideoState(HomeViewModel previous)
    {
        LinkedVideoUrl = previous.LinkedVideoUrl;
        LinkedVideoDateText = previous.LinkedVideoDateText;
        SelectedLinkedVideoDestination = LinkedVideoDestinations.FirstOrDefault(option => option.Source == previous.SelectedLinkedVideoDestination?.Source);
        if (previous._inspectedLinkedVideo is not null && previous.LinkedVideoUrl == LinkedVideoUrl)
        {
            SetInspectedLinkedVideo(previous._inspectedLinkedVideo);
            SelectedLinkedVideoFormat = LinkedVideoFormats.FirstOrDefault(format => format == previous.SelectedLinkedVideoFormat);
        }
        LinkedVideoStatus = previous.LinkedVideoStatus;
        IsLinkedVideoWorkspace = previous.IsLinkedVideoWorkspace;
    }
    public void SetInspectedLinkedVideo(LinkedVideo video)
    {
        _inspectedLinkedVideo = video;
        LinkedVideoFormats.Clear();
        foreach (var format in video.Formats) LinkedVideoFormats.Add(format);
        SelectedLinkedVideoFormat = video.Formats.FirstOrDefault(format => format.Height >= 480) ?? video.Formats.FirstOrDefault();
        LinkedVideoStatus = $"{video.Formats.Count} qualidade(s) MP4 disponíveis.";
        OnPropertyChanged(nameof(LinkedVideoTitle));
        OnPropertyChanged(nameof(LinkedVideoPublishedDate));
        OnPropertyChanged(nameof(CanQueueLinkedVideo));
        OnPropertyChanged(nameof(HasLinkedVideo));
        OnPropertyChanged(nameof(LinkedVideoSummary));
    }
    public void ClearInspectedLinkedVideo()
    {
        _inspectedLinkedVideo = null;
        SelectedLinkedVideoFormat = null;
        LinkedVideoFormats.Clear();
        OnPropertyChanged(nameof(HasLinkedVideo));
        OnPropertyChanged(nameof(LinkedVideoSummary));
        OnPropertyChanged(nameof(LinkedVideoTitle));
        OnPropertyChanged(nameof(LinkedVideoPublishedDate));
        OnPropertyChanged(nameof(CanQueueLinkedVideo));
    }
    public LinkedVideoDownloadRequest CreateLinkedVideoRequest()
    {
        if (!CanQueueLinkedVideo || _inspectedLinkedVideo is null || SelectedLinkedVideoFormat is null || SelectedLinkedVideoDestination is null ||
            !DateOnly.TryParseExact(LinkedVideoDateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidOperationException("Consulte um vídeo e informe programa, qualidade e data de uso válidos.");
        return new LinkedVideoDownloadRequest(_inspectedLinkedVideo, SelectedLinkedVideoFormat, SelectedLinkedVideoDestination.Source, date);
    }
    partial void OnSelectedAvailabilityChanged(string value) => ApplyFilters();
    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedCatalogItemChanged(CatalogCard? value)
    {
        OnPropertyChanged(nameof(SelectedItemTitle));
        OnPropertyChanged(nameof(SelectedItemDetails));
        OnPropertyChanged(nameof(SelectedItemPath));
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(IsSelectedItemPinned));
        OnPropertyChanged(nameof(SelectedItemPinActionLabel));
        DetailsOpen = value is not null;
        OnPropertyChanged(nameof(ShowCatalogDetails));
        OnPropertyChanged(nameof(SelectedPlaybackLabel));
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
        IsSynchronizationIndeterminate = progress.Percentage is null;
        SyncProgressPercent = progress.Percentage ?? 0;
        SyncProgressLabel = progress.Percentage is { } percentage
            ? $"{progress.Item.Title}: {progress.Stage} ({percentage:0.0}%)"
            : $"{progress.Item.Title}: {progress.Stage}";
        SynchronizationMessage = SyncProgressLabel;
        if (IsLibraryWorkspace) OperationMessage = SyncProgressLabel;
        if (progress.Item.SyncState == SyncState.Ready) MarkItemAsReady(progress.Item);
    }

    public void UpdateSynchronizationQueue(SynchronizationQueueSnapshot snapshot)
    {
        IsQueueActive = snapshot.IsProcessing;
        SynchronizationQueueItems.Clear();
        foreach (var entry in snapshot.Entries)
        {
            var pending = entry.State is SynchronizationQueueState.Waiting or SynchronizationQueueState.Running;
            SynchronizationQueueItems.Add(new SynchronizationQueueCard(entry.SourceName, GetQueueStateLabel(entry.State), entry.Message, pending, entry.Diagnostic));
        }

        OnPropertyChanged(nameof(HasSynchronizationQueueItems));
        OnPropertyChanged(nameof(DownloadsLabel));

        var active = snapshot.Entries.FirstOrDefault(entry => entry.State == SynchronizationQueueState.Running);
        if (active is not null)
        {
            IsBusy = true;
            SyncProgressPercent = active.Percentage ?? 0;
            IsSynchronizationIndeterminate = active.Percentage is null;
            SyncProgressLabel = active.Message;
            SynchronizationMessage = $"{active.SourceName}: {active.Message}";
            if (IsLibraryWorkspace) OperationMessage = SynchronizationMessage;
        }
        else
        {
            IsBusy = false;
            IsSynchronizationIndeterminate = false;
            var last = snapshot.Entries.LastOrDefault();
            if (last is not null) { SynchronizationMessage = $"{last.SourceName}: {last.Message}"; if (IsLibraryWorkspace) OperationMessage = SynchronizationMessage; }
        }
        OnPropertyChanged(nameof(CanQueueSelectedSource));
    }

    public void MarkItemAsReady(ContentItem item)
    {
        var hadSelection = SelectedCatalogItem is not null;
        var index = _allCatalogItems.FindIndex(card => card.Id == item.Id);
        var card = MapItem(item);
        if (index >= 0) _allCatalogItems[index] = card;
        else _allCatalogItems.Add(card);
        ApplyFilters();
        if (!hadSelection && CatalogItems.Contains(card))
        {
            SelectedCatalogItem = card;
            DetailsOpen = false;
        }
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

    public void MarkItemPinned(ContentItem item)
    {
        var index = _allCatalogItems.FindIndex(card => card.Id == item.Id);
        if (index < 0) return;
        var card = MapItem(item);
        _allCatalogItems[index] = card;
        ApplyFilters();
        SelectedCatalogItem = card;
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

        var selectedId = SelectedCatalogItem?.Id;
        CatalogItems.Clear();
        foreach (var item in filtered) CatalogItems.Add(item);
        SelectedCatalogItem = CatalogItems.FirstOrDefault(item => item.Id == selectedId);
        OnPropertyChanged(nameof(CatalogSummary));
        OnPropertyChanged(nameof(IsCatalogEmpty));
        OnPropertyChanged(nameof(CatalogEmptyMessage));
    }

    private static CatalogCard MapItem(ContentItem item) => new(
        item.Id,
        item.Title,
        GetSourceName(item.Source),
        item.ScheduledDate.ToString("dd/MM/yyyy"),
        GetCatalogStatus(item),
        item.LocalPath,
        item.IsReadyOffline ? "▶" : item.SyncState == SyncState.OnlineOnly ? "◌" : "↓",
        item.PlayCount == 0 ? string.Empty : $"Reproduzido {item.PlayCount}×",
        item.IsPinned);

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

public sealed partial class SourceCard(ContentSource source, string name, string syncPolicy, string status) : ObservableObject
{
    public ContentSource Source { get; } = source;
    public string Name { get; } = name;
    public string SyncPolicy { get; } = syncPolicy;
    public string Status { get; } = status;
    [ObservableProperty] private bool isSelected;
}
public sealed record LinkedVideoDestinationOption(ContentSource Source, string Label);
public sealed record CatalogCard(string Id, string Title, string SourceName, string ScheduledDate, string Status, string? LocalPath, string ThumbnailGlyph, string PlaybackLabel = "", bool IsPinned = false)
{
    // Compatibilidade com consumidores que já exibiam a coluna "Source" da lista anterior.
    public string Source => SourceName;
}
public sealed partial class ScheduleCard(string id, string title, string sourceName, string status) : ObservableObject
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public string SourceName { get; } = sourceName;
    public string Status { get; } = status;
    [ObservableProperty] private int position;
    [ObservableProperty] private bool canMoveUp;
    [ObservableProperty] private bool canMoveDown;
}
public sealed record PlaybackScreenOption(string Label, int ScreenNumber, bool IsPrimary = false, string MonitorKey = "");
public sealed record SynchronizationQueueCard(string SourceName, string State, string Details, bool IsPending, SynchronizationDiagnostic? Diagnostic = null)
{
    public bool HasDiagnostic => Diagnostic is not null;
}
