using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinalo.Application.Library;
using Sinalo.Application.Catalog;
using Sinalo.Application.Playback;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public sealed record LibraryOriginOption(string Value, string Label) { public override string ToString() => Label; }
public sealed record LibrarySortOption(LibrarySort Value, string Label) { public override string ToString() => Label; }
public sealed partial class LibraryViewModel(ILibraryRepository repository, ILibraryImportService importer,
    ILibraryFileService files, LibraryPlaybackService playback, IContentCatalog catalog,
    Sinalo.Application.Storage.IContentDeletionService deletion,
    Func<Task<PlaybackLaunchOptions?>>? resolveOutput = null, Func<bool>? presentationOpen = null) : ObservableObject
{
    private CancellationTokenSource? _importCancellation;
    private TaskCompletionSource? _importFinished;
    private long _loadGeneration;
    private int _page;
    private int _total;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private LibraryOriginOption origin = new("Todos", "Todas as origens");
    [ObservableProperty] private LibrarySortOption sort = new(LibrarySort.Name, "Nome");
    [ObservableProperty] private LibraryMedia? selected;
    [ObservableProperty] private string editName = "";
    [ObservableProperty] private string editDate = "";
    [ObservableProperty] private bool useOriginal;
    [ObservableProperty] private bool includeSubfolders = true;
    [ObservableProperty] private bool deleteCopy;
    [ObservableProperty] private bool isImporting;
    [ObservableProperty] private string message = "Vídeos MP4 locais e dos programas. Áudio e imagens serão entregues em etapas futuras.";
    [ObservableProperty] private double progress;
    public ObservableCollection<LibraryMedia> Items { get; } = [];
    public IReadOnlyList<LibraryOriginOption> Origins { get; } = [new("Todos", "Todas as origens"), new("Importados", "Importados"), new("0", "Informativo das Missões"), new("1", "Provai e Vede"), new("2", "Minuto de Saúde")];
    public IReadOnlyList<LibrarySortOption> Sorts { get; } = [new(LibrarySort.Name, "Nome"), new(LibrarySort.AddedAt, "Data de inclusão"), new(LibrarySort.UsageDate, "Data de uso")];
    public string PageLabel => $"Página {_page + 1} · {_total} vídeo(s)";
    public bool HasSelection => Selected is not null;
    public bool IsImported => Selected?.IsImported == true;
    public bool CanDeleteCopy => IsImported && Selected?.StorageMode == MediaStorageMode.Managed;
    public bool CanEditDate => IsImported;
    public string SelectedDetails => Selected is null ? "Selecione um vídeo" : $"Vídeo MP4 · {Selected.OriginLabel} · {Selected.StorageLabel}\n{Selected.LocalPath}\n{(Selected.CanPlay ? "Disponível offline" : "Arquivo ausente ou inválido: use Localizar arquivo")} · Reproduzido {Selected.PlayCount} vez(es)";
    partial void OnSelectedChanged(LibraryMedia? oldValue, LibraryMedia? newValue)
    {
        var value = newValue;
        if (!IsEditing || oldValue?.Id != value?.Id)
        {
            EditName = value?.Name ?? ""; EditDate = value?.UsageDate?.ToString("dd/MM/yyyy") ?? ""; DeleteCopy = false;
            IsEditing = false;
        }
        if (value is null) DetailsOpen = false;
        else if (IsWideLayout && oldValue?.Id != value.Id) DetailsOpen = true;
        foreach (var property in new[] { nameof(HasSelection), nameof(IsImported), nameof(CanDeleteCopy), nameof(CanEditDate), nameof(SelectedDetails) }) OnPropertyChanged(property);
        NotifyWorkspace();
    }
    [RelayCommand]
    public async Task RefreshAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        var id = Selected?.Id;
        var query = new LibraryQuery(Search, Origin.Value, Sort.Value, _page);
        try
        {
            var page = await Task.Run(() => repository.QueryAsync(query));
            if (generation != _loadGeneration) return;
            if (id is not null && !page.Items.Any(item => item.Id == id) && !await ResolvePendingChangesAsync()) return;
            if (generation != _loadGeneration) return;
            _total = page.Total;
            IsRefreshing = true;
            Items.Clear(); foreach (var media in page.Items) Items.Add(media);
            Selected = Items.FirstOrDefault(item => item.Id == id);
            IsRefreshing = false;
            NotifyWorkspace();
            OnPropertyChanged(nameof(PageLabel));
        }
        catch (Exception exception) { Message = exception.Message; }
    }
    [RelayCommand] private async Task SearchNowAsync() { if (!await ResolvePendingChangesAsync()) return; _page = 0; await RefreshAsync(); }
    [RelayCommand] private async Task NextPageAsync() { if ((_page + 1) * 50 < _total && await ResolvePendingChangesAsync()) { _page++; await RefreshAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (_page > 0 && await ResolvePendingChangesAsync()) { _page--; await RefreshAsync(); } }
    public async Task ImportAsync(IReadOnlyList<string> paths)
    {
        if (IsImporting) return;
        _importCancellation = new(); _importFinished = new(TaskCreationOptions.RunContinuationsAsynchronously); IsImporting = true; HasImportResult = false; ImportFailures = "";
        try
        {
            var request = new LibraryImportRequest(paths, UseOriginal ? MediaStorageMode.Referenced : MediaStorageMode.Managed, IncludeSubfolders);
            var result = await importer.ImportAsync(request, new Progress<LibraryImportProgress>(update => { Progress = update.Percentage; ImportMessage = $"{update.Stage}: {update.File}"; }), _importCancellation.Token);
            _page = 0; await RefreshAsync();
            ImportedCount = result.Imported; ExistingCount = result.Existing; IgnoredCount = result.Ignored; FailedCount = result.Failures.Count;
            ImportFailures = string.Join("\n", result.Failures); HasImportResult = true;
            ImportMessage = result.Cancelled ? "Importação cancelada. Os vídeos já concluídos foram preservados." : "Importação concluída.";
            Message = $"{(result.Cancelled ? "Cancelado" : "Concluído")}: {result.Imported} importado(s), {result.Existing} existente(s), {result.Ignored} ignorado(s), {result.Failures.Count} falha(s)." + (result.Failures.Count > 0 ? "\n" + string.Join("\n", result.Failures) : "");
            ActionMessage = "";
        }
        catch (Exception exception) { ImportMessage = Message = $"Não foi possível concluir a importação: {exception.Message}"; ActionMessage = ""; }
        finally { IsImporting = false; _importCancellation.Dispose(); _importCancellation = null; _importFinished.TrySetResult(); }
    }
    public Task WhenImportIdleAsync() => _importFinished?.Task ?? Task.CompletedTask;
    [RelayCommand] public void CancelImport() => _importCancellation?.Cancel();
    [RelayCommand] private async Task SaveNameAsync() => await SafeAsync(async () =>
    {
        if (Selected is null) return;
        DateOnly? date = null;
        if (!string.IsNullOrWhiteSpace(EditDate))
        {
            if (!DateOnly.TryParseExact(EditDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw new FormatException("Informe a data em DD/MM/AAAA ou deixe em branco.");
            date = parsed;
        }
        await repository.RenameAsync(Selected.Id, EditName, date); IsEditing = false; await RefreshAsync(); Message = "Alterações salvas. O arquivo físico não foi renomeado.";
    });
    [RelayCommand] private async Task PlayAsync() => await SafeAsync(async () =>
    {
        if (Selected is null) return;
        if (presentationOpen?.Invoke() == true) throw new InvalidOperationException("Feche a apresentação antes de reproduzir vídeos.");
        var output = resolveOutput is null ? new PlaybackLaunchOptions(1) : await resolveOutput();
        if (output is null) throw new InvalidOperationException("A tela de saída não está disponível.");
        var result = await playback.PlayAsync(Selected.Id, output); Message = result.Message; await RefreshAsync();
    });
    [RelayCommand] public async Task RemoveAsync() => await SafeAsync(async () =>
    {
        if (Selected is null) return;
        if (Selected.ContentItemId is { } linked) await deletion.DeleteAsync(linked);
        else await files.RemoveAsync(Selected.Id, DeleteCopy);
        await RefreshAsync(); Message = "Item removido. Arquivos referenciados e cópias não selecionadas para exclusão foram preservados.";
    });
    public async Task LocateAsync(string path) => await SafeAsync(async () =>
    { if (Selected is null) return; await files.LocateAsync(Selected.Id, path); await RefreshAsync(); Message = "Arquivo localizado e validado."; });
    [RelayCommand] private async Task TogglePinAsync() => await SafeAsync(async () =>
    { if (Selected?.ContentItemId is not { } linked) return; await catalog.SetPinnedAsync(linked, !Selected.IsPinned); await RefreshAsync(); });
    private async Task SafeAsync(Func<Task> action)
    { try { await action(); } catch (Exception exception) { Message = exception.Message; } }
}
