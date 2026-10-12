using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public enum LibraryEditDecision { Save, Discard, Cancel }

public sealed partial class LibraryViewModel
{
    [ObservableProperty] private bool isImportTab;
    [ObservableProperty] private bool detailsOpen;
    [ObservableProperty] private bool isWideLayout;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool filtersExpanded;
    [ObservableProperty] private bool presentationIsOpen;
    [ObservableProperty] private bool hasOutputScreen = true;
    [ObservableProperty] private string outputScreenLabel = "tela selecionada";
    [ObservableProperty] private string contentFolder = "";
    [ObservableProperty] private string actionMessage = "";
    [ObservableProperty] private string importMessage = "Escolha como armazenar os vídeos e selecione os arquivos ou uma pasta.";
    [ObservableProperty] private bool hasImportResult;
    [ObservableProperty] private int importedCount;
    [ObservableProperty] private int existingCount;
    [ObservableProperty] private int ignoredCount;
    [ObservableProperty] private int failedCount;
    [ObservableProperty] private string importFailures = "";
    public bool IsRefreshing { get; private set; }
    public Func<LibraryEditDecision>? ConfirmPendingChanges { get; set; }
    public bool CopyIntoSinalo { get => !UseOriginal; set => UseOriginal = !value; }
    public bool CanImport => !IsImporting;
    public bool HasImportFailures => FailedCount > 0;
    public bool ShowVideos => !IsImportTab;
    public bool ShowList => ShowVideos && (!DetailsOpen || IsWideLayout);
    public bool ShowDetails => ShowVideos && DetailsOpen && HasSelection;
    public bool ShowColumnHeaders => IsWideLayout;
    public bool ShowCompactRows => !IsWideLayout;
    public bool ShowFilters => IsWideLayout || FiltersExpanded;
    public bool ShowEmpty => Items.Count == 0;
    public bool CanPrevious => _page > 0;
    public bool CanNext => (_page + 1) * 50 < _total;
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || Origin.Value != "Todos";
    public string CountLabel => $"{_total} {(IsFiltered ? "resultado(s)" : "vídeo(s)")}";
    public string EmptyTitle => IsFiltered ? "Nenhum vídeo corresponde à pesquisa" : "Sua biblioteca está vazia";
    public string EmptyDescription => IsFiltered ? "Tente outro nome ou data, ou limpe os filtros." : "Importe vídeos MP4 ou baixe vídeos de um dos programas na navegação.";
    public bool IsProgramVideo => HasSelection && !IsImported;
    public bool ShowReading => !IsEditing;
    public bool NeedsLocate => HasSelection && Selected?.CanPlay != true;
    public bool CanPlaySelected => Selected?.CanPlay == true && !PresentationIsOpen && HasOutputScreen;
    public string PlayLabel => HasOutputScreen ? $"Reproduzir na {OutputScreenLabel}" : "Nenhuma tela de saída";
    public string UsageDateLabel => Selected?.UsageDate?.ToString("dd/MM/yyyy") ?? "Sem data de uso";
    public string PlaybackCountLabel => $"{Selected?.PlayCount ?? 0} reprodução(ões)";
    public string PinLabel => Selected?.IsPinned == true ? "Desafixar vídeo" : "Fixar vídeo";
    public string RemoveLabel => IsImported ? "Remover da biblioteca" : "Excluir vídeo baixado";
    public string RemovalExplanation => IsImported
        ? "Remover o cadastro mantém o arquivo no disco. Um original referenciado nunca será apagado."
        : "O arquivo baixado será excluído. Ele poderá ser baixado novamente conforme a configuração do programa.";
    public bool HasPendingChanges => IsEditing && Selected is not null &&
        (EditName != Selected.Name || EditDate != (Selected.UsageDate?.ToString("dd/MM/yyyy") ?? ""));
    partial void OnMessageChanged(string value) => ActionMessage = value;
    partial void OnUseOriginalChanged(bool value) => OnPropertyChanged(nameof(CopyIntoSinalo));
    partial void OnIsImportingChanged(bool value) => OnPropertyChanged(nameof(CanImport));
    partial void OnIsImportTabChanged(bool value) => NotifyWorkspace();
    partial void OnDetailsOpenChanged(bool value) => NotifyWorkspace();
    partial void OnIsWideLayoutChanged(bool value) => NotifyWorkspace();
    partial void OnIsEditingChanged(bool value) => NotifyWorkspace();
    partial void OnFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(ShowFilters));
    partial void OnPresentationIsOpenChanged(bool value) => NotifyWorkspace();
    partial void OnOutputScreenLabelChanged(string value) => OnPropertyChanged(nameof(PlayLabel));
    partial void OnHasOutputScreenChanged(bool value) { OnPropertyChanged(nameof(PlayLabel)); OnPropertyChanged(nameof(CanPlaySelected)); }
    partial void OnFailedCountChanged(int value) => OnPropertyChanged(nameof(HasImportFailures));
    private void NotifyWorkspace()
    {
        foreach (var name in new[] { nameof(ShowVideos), nameof(ShowList), nameof(ShowDetails), nameof(ShowColumnHeaders), nameof(ShowCompactRows),
            nameof(ShowEmpty), nameof(ShowFilters), nameof(CanPrevious), nameof(CanNext), nameof(IsFiltered), nameof(CountLabel), nameof(EmptyTitle), nameof(EmptyDescription),
            nameof(IsProgramVideo), nameof(ShowReading), nameof(NeedsLocate), nameof(CanPlaySelected), nameof(UsageDateLabel), nameof(PlaybackCountLabel),
            nameof(PinLabel), nameof(RemoveLabel), nameof(RemovalExplanation) }) OnPropertyChanged(name);
    }
    [RelayCommand] private void ShowImports() => IsImportTab = true;
    [RelayCommand] private void ToggleFilters() => FiltersExpanded = !FiltersExpanded;
    [RelayCommand] private void ShowVideoList() => IsImportTab = false;
    [RelayCommand] private void ShowDetailsPanel() { if (HasSelection) DetailsOpen = true; }
    [RelayCommand] private void EditInformation() { if (HasSelection) IsEditing = true; }
    [RelayCommand] private void CancelEditing()
    {
        EditName = Selected?.Name ?? ""; EditDate = Selected?.UsageDate?.ToString("dd/MM/yyyy") ?? ""; IsEditing = false;
    }
    public async Task<bool> ResolvePendingChangesAsync()
    {
        if (!HasPendingChanges) return true;
        var decision = ConfirmPendingChanges?.Invoke() ?? LibraryEditDecision.Cancel;
        if (decision == LibraryEditDecision.Cancel) return false;
        if (decision == LibraryEditDecision.Discard) { CancelEditing(); return true; }
        await SaveNameCommand.ExecuteAsync(null);
        return !HasPendingChanges;
    }
    public async Task<bool> SelectVideoAsync(LibraryMedia? media)
    {
        if (media?.Id == Selected?.Id) return true;
        if (!await ResolvePendingChangesAsync()) return false;
        Selected = media; ActionMessage = ""; return true;
    }
    [RelayCommand] private async Task CloseDetailsAsync()
    { if (await ResolvePendingChangesAsync()) { IsEditing = false; DetailsOpen = false; } }
    [RelayCommand] private async Task ClearFiltersAsync()
    { Search = ""; Origin = Origins[0]; _page = 0; await RefreshAsync(); }
}
