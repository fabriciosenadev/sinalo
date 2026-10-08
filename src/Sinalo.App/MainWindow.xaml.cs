using System.Windows;
using System.Net.Http;
using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Application.Appearance;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;
using Sinalo.App.ViewModels;
using Sinalo.Application.Playback;
using Sinalo.Application.Updates;
using Sinalo.Application.Monitors;
using Sinalo.Application.Presentation;
using Sinalo.Application.Timer;
using Sinalo.Application.Raffle;
using Sinalo.Application.WorshipTimer;
using System.Windows.Threading;

namespace Sinalo.App;

public partial class MainWindow : Window
{
    private SynchronizationQueue? _synchronizationQueue;
    private readonly DispatcherTimer _timerRefresh = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _updateCheckTimer = new() { Interval = TimeSpan.FromHours(6) };
    private readonly SemaphoreSlim _updateCheckGate = new(1, 1);
    private readonly CancellationTokenSource _updateCheckCancellation = new();
    public ISinaloConfigurationService? ConfigurationService { get; init; }
    public IContentPathConfigurationService? ContentPathConfigurationService { get; init; }
    public IContentPathMigrationService? ContentPathMigrationService { get; init; }
    public IContentCleanupConfigurationService? ContentCleanupConfigurationService { get; init; }
    public IContentCleanupService? ContentCleanupService { get; init; }
    public IApplicationUpdateService? ApplicationUpdateService { get; init; }
    public IUpdateInstallerLauncher? UpdateInstallerLauncher { get; init; }
    public IThemePreferenceService? ThemePreferenceService { get; init; }
    public SystemThemeService? ThemeService { get; init; }
    private DownloadedUpdate? _downloadedUpdate;
    public IPlaybackConfigurationService? PlaybackConfigurationService { get; init; }
    public IMonitorService? MonitorService { get; init; }
    public IPresentationOutputService? PresentationOutputService { get; init; }
    public ITimerConfigurationService? TimerConfigurationService { get; init; }
    public IWorshipTimerConfigurationService? WorshipTimerConfigurationService { get; init; }
    public IRaffleConfigurationService? RaffleConfigurationService { get; init; }
    public ContentDiscoveryService? DiscoveryService { get; init; }
    public IContentCatalog? ContentCatalog { get; init; }
    public IContentDeletionService? ContentDeletionService { get; init; }
    public IContentStorageSpaceService? ContentStorageSpaceService { get; init; }
    public IWorshipTimerAudioPlayer? WorshipTimerAudioPlayer { get; init; }
    public ISynchronizationDiagnosticStore? SynchronizationDiagnosticStore { get; init; }
    public ProvaiEVedeSynchronizationService? ProvaiEVedeSynchronizationService { get; init; }
    public MissionsSynchronizationService? MissionsSynchronizationService { get; init; }
    public HealthSynchronizationService? HealthSynchronizationService { get; init; }
    public ManualContentSynchronizationService? ManualSynchronizationService { get; init; }
    public ILinkedVideoService? LinkedVideoService { get; init; }
    public PlaybackService? PlaybackService { get; init; }
    public IAsyncDisposable? PlaybackRuntime { get; init; }
    public SynchronizationQueue? SynchronizationQueue
    {
        get => _synchronizationQueue;
        set
        {
            if (_synchronizationQueue is not null) _synchronizationQueue.Changed -= SynchronizationQueue_Changed;
            _synchronizationQueue = value;
            if (_synchronizationQueue is not null) _synchronizationQueue.Changed += SynchronizationQueue_Changed;
        }
    }
    public MainWindow()
    {
        InitializeComponent();
        InitializeWorkspaceUi();
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, SystemThemeService.IsWindowsDarkTheme());
        _timerRefresh.Tick += TimerRefresh_Tick;
        _updateCheckTimer.Tick += PeriodicUpdateCheck_Tick;
        Loaded += (_, _) => _timerRefresh.Start();
        Closing += (_, _) => SynchronizationQueue?.CancelAll();
        Closed += (_, _) =>
        {
            _timerRefresh.Stop();
            _updateCheckTimer.Stop();
            _updateCheckCancellation.Cancel();
            WorshipTimerAudioPlayer?.Stop();
        };
    }

    private async void ConfigureSources_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigurationService is null) return;
        var window = new SettingsWindow(ConfigurationService, ContentPathConfigurationService, ContentPathMigrationService, ThemePreferenceService, ThemeService, ContentCleanupConfigurationService) { Owner = this };
        if (sender is System.Windows.Controls.MenuItem { Tag: "ProgramSearchRules" }) window.ShowProgramSettings();
        window.ShowDialog();
        if (window.Saved)
        {
            var previous = DataContext as HomeViewModel;
            var viewModel = new ViewModels.HomeViewModel(
                new Infrastructure.SaturdayWindowService(),
                new Infrastructure.LocalSinaloPathService(),
                await ConfigurationService.LoadSourcesAsync(),
                await LoadCatalogAsync(),
                previous?.PlaybackScreens,
                previous?.SelectedPlaybackScreen?.ScreenNumber,
                timer: previous?.Timer,
                raffle: previous?.Raffle,
                worshipTimer: previous?.WorshipTimer);
            RestoreFilters(viewModel, previous);
            DataContext = viewModel;
        }
    }

    private void OpenReleaseNotes_Click(object sender, RoutedEventArgs e) =>
        new ReleaseNotesWindow(ThemeService) { Owner = this }.ShowDialog();

    public void StartPeriodicUpdateChecks() => _updateCheckTimer.Start();

    public async Task CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (ApplicationUpdateService is null || DataContext is not HomeViewModel viewModel) return;
        if (!_updateCheckGate.Wait(0)) return;
        try
        {
            var versionText = GetType().Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var update = await ApplicationUpdateService.CheckAsync(Version.Parse(versionText), cancellationToken);
            if (update is null) return;
            if (_downloadedUpdate is { } downloaded && downloaded.Update.Version >= update.Version)
            {
                viewModel.ReportUpdateReady(downloaded.Update.Version);
                return;
            }
            viewModel.ReportUpdateAvailable(update.Version);
            _downloadedUpdate = await ApplicationUpdateService.DownloadAsync(
                update,
                new ImmediateProgress<UpdateDownloadProgress>(progress => viewModel.ReportUpdateProgress(progress.Percentage)),
                cancellationToken);
            viewModel.ReportUpdateReady(update.Version);
        }
        catch (OperationCanceledException) { }
        catch
        {
            viewModel.ReportUpdateFailure();
        }
        finally
        {
            _updateCheckGate.Release();
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void PeriodicUpdateCheck_Tick(object? sender, EventArgs e) => await CheckForUpdateAsync(_updateCheckCancellation.Token);

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadedUpdate is null || UpdateInstallerLauncher is null || (DataContext as HomeViewModel)?.IsInstallingUpdate == true) return;
        if (DataContext is HomeViewModel installing) { installing.IsInstallingUpdate = true; installing.IsUpdateReady = false; }
        try
        {
            if (DataContext is HomeViewModel viewModel) viewModel.UpdateMessage = "Preparando atualização. Encerrando reprodução e sincronizações...";
            await PrepareForShutdownAsync();
            UpdateInstallerLauncher.Launch(_downloadedUpdate.InstallerPath);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            if (DataContext is HomeViewModel viewModel) { viewModel.UpdateMessage = $"Não foi possível iniciar a atualização: {exception.Message}"; viewModel.IsInstallingUpdate = false; viewModel.IsUpdateReady = true; }
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async Task PrepareForShutdownAsync()
    {
        _updateCheckTimer.Stop();
        _updateCheckCancellation.Cancel();
        SynchronizationQueue?.CancelAll();
        if (SynchronizationQueue is not null)
        {
            try { await SynchronizationQueue.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { }
        }

        if (PresentationOutputService is not null)
        {
            try { await PresentationOutputService.CloseAsync(); }
            catch { }
        }
        if (PlaybackRuntime is not null)
        {
            try { await PlaybackRuntime.DisposeAsync(); }
            catch { }
        }
    }

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
    {
        await RefreshSourceAsync(Sinalo.Domain.ContentSource.ProvaiEVede);
    }

    private async void SynchronizeProvaiEVede_Click(object sender, RoutedEventArgs e)
    {
        await SynchronizeSourceAsync(Sinalo.Domain.ContentSource.ProvaiEVede);
    }

    private async void UpdateAndSynchronizeSelectedSource_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        if (viewModel.SelectedSource == "Provai e Vede") await EnqueueSynchronizationAsync(Sinalo.Domain.ContentSource.ProvaiEVede);
        if (viewModel.SelectedSource == "Informativo das Missões") await EnqueueSynchronizationAsync(Sinalo.Domain.ContentSource.Missions);
        if (viewModel.SelectedSource == "Minuto de Saúde") await EnqueueSynchronizationAsync(Sinalo.Domain.ContentSource.Health);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void DiscoverSelectedSource_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || ConfigurationService is null || DiscoveryService is null || ContentCatalog is null) return;
        var source = viewModel.SelectedSource switch
        {
            "Provai e Vede" => Sinalo.Domain.ContentSource.ProvaiEVede,
            "Informativo das Missões" => Sinalo.Domain.ContentSource.Missions,
            "Minuto de Saúde" => Sinalo.Domain.ContentSource.Health,
            _ => (Sinalo.Domain.ContentSource?)null
        };
        if (source is null) return;

        try
        {
            viewModel.OperationMessage = $"Procurando vídeos em {viewModel.SelectedSource}...";
            var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source.Value);
            var items = await DiscoveryService.RefreshAsync(configuration);
            var defaults = SynchronizationCandidateSelector.Select(source.Value, items, configuration.ResolvedDownloadSelection, new SaturdayWindowService(), DateOnly.FromDateTime(DateTime.Today))
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var selections = items.OrderBy(item => item.ScheduledDate).Select(item => new ManualVideoSelectionItem(item, defaults.Contains(item.Id))).ToArray();
            var dialog = new ManualVideoSelectionWindow(configuration.DisplayName, selections, ThemeService) { Owner = this };
            if (dialog.ShowDialog() != true) { viewModel.OperationMessage = "Seleção manual cancelada. Nenhum download foi iniciado."; return; }
            var selectedIds = dialog.SelectedItemIds;
            if (selectedIds.Count == 0) return;
            if (ContentStorageSpaceService is not null)
            {
                var assessment = await ContentStorageSpaceService.AssessAsync(items.Where(item => selectedIds.Contains(item.Id)).ToArray());
                if (!assessment.HasSufficientSpace) { viewModel.OperationMessage = new InsufficientStorageSpaceException(assessment).Message; return; }
            }
            var result = SynchronizationQueue?.Enqueue(new SynchronizationQueueRequest(configuration, selectedIds));
            viewModel.OperationMessage = result?.Message ?? "Não foi possível adicionar a seleção à fila.";
        }
        catch (HttpRequestException)
        {
            viewModel.OperationMessage = $"Não foi possível consultar {viewModel.SelectedSource}. Verifique a conexão e tente novamente.";
        }
        catch (Exception exception)
        {
            var diagnostic = SynchronizationFailureClassifier.Classify(exception, source.Value, viewModel.SelectedSource, string.Empty, SynchronizationStage.Discovery);
            viewModel.OperationMessage = diagnostic.FriendlyMessage;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private void LinkedVideoWorkspace_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.SelectLinkedVideoWorkspace();

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void InspectLinkedVideo_Click(object sender, RoutedEventArgs e)
    {
        if (LinkedVideoService is null || DataContext is not HomeViewModel viewModel || !viewModel.CanInspectLinkedVideo) return;
        var url = viewModel.LinkedVideoUrl;
        viewModel.IsInspectingLinkedVideo = true;
        viewModel.ClearInspectedLinkedVideo();
        viewModel.LinkedVideoStatus = "Consultando informações do vídeo...";
        try
        {
            var video = await LinkedVideoService.InspectAsync(url);
            if (viewModel.LinkedVideoUrl == url) viewModel.SetInspectedLinkedVideo(video);
        }
        catch (Exception exception) { if (viewModel.LinkedVideoUrl == url) viewModel.LinkedVideoStatus = exception.Message; }
        finally { viewModel.IsInspectingLinkedVideo = false; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void QueueLinkedVideo_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigurationService is null || ContentCatalog is null || SynchronizationQueue is null || DataContext is not HomeViewModel viewModel || !viewModel.CanQueueLinkedVideo) return;
        try
        {
            var selected = viewModel.CreateLinkedVideoRequest();
            var existing = await ContentCatalog.FindByIdAsync(selected.ItemId);
            if (existing?.IsReadyOffline == true && existing.LocalPath is { } localPath && File.Exists(localPath))
            {
                viewModel.OperationMessage = "Este vídeo já está pronto offline no programa escolhido.";
                viewModel.LinkedVideoStatus = viewModel.OperationMessage;
                return;
            }
            var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == selected.Destination);
            viewModel.OperationMessage = SynchronizationQueue.Enqueue(new SynchronizationQueueRequest(configuration, LinkedVideo: selected)).Message;
            viewModel.LinkedVideoStatus = viewModel.OperationMessage;
        }
        catch (Exception exception) { viewModel.LinkedVideoStatus = $"Não foi possível adicionar o vídeo à fila: {exception.Message}"; }
    }

    internal async void CancelSynchronizationQueue_Click(object sender, RoutedEventArgs e)
    {
        SynchronizationQueue?.CancelAll();
        await Task.CompletedTask;
    }

    public SynchronizationQueue CreateSynchronizationQueue() => new(ExecuteQueuedSynchronizationAsync, SynchronizationDiagnosticStore);

    private async Task EnqueueSynchronizationAsync(Sinalo.Domain.ContentSource source)
    {
        if (SynchronizationQueue is null || ConfigurationService is null || DiscoveryService is null || ContentCatalog is null || DataContext is not HomeViewModel viewModel) return;
        var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
        try
        {
            if (ContentCleanupService is not null)
            {
                var cleanup = await ContentCleanupService.CleanIfDueAsync(DateOnly.FromDateTime(DateTime.Today));
                if (cleanup.WasRun && cleanup.RemovedCount > 0)
                {
                    viewModel.OperationMessage = $"Limpeza automática: {cleanup.RemovedCount} vídeo(s) removido(s), {FormatBytes(cleanup.ReclaimedBytes)} recuperados.";
                    ReplaceHomeViewModel(await ConfigurationService.LoadSourcesAsync(), await LoadCatalogAsync(), viewModel.OperationMessage);
                    viewModel = (HomeViewModel)DataContext;
                }
            }
            viewModel.OperationMessage = $"Consultando o site oficial de {configuration.DisplayName}...";
            await DiscoveryService.RefreshAsync(configuration);
            var candidates = SynchronizationCandidateSelector.Select(source, await ContentCatalog.ListBySourceAsync(source), configuration.ResolvedDownloadSelection, new SaturdayWindowService(), DateOnly.FromDateTime(DateTime.Today));
            if (ContentStorageSpaceService is not null)
            {
                var assessment = await ContentStorageSpaceService.AssessAsync(candidates);
                if (!assessment.HasSufficientSpace)
                {
                    viewModel.OperationMessage = new InsufficientStorageSpaceException(assessment).Message;
                    return;
                }
                if (assessment.HasUnknownSizes) viewModel.OperationMessage = "O tamanho de alguns vídeos não foi informado. O espaço será acompanhado durante o download.";
            }
        }
        catch (HttpRequestException)
        {
            viewModel.OperationMessage = $"Não foi possível consultar {configuration.DisplayName}. Verifique a conexão e tente novamente.";
            return;
        }
        var result = SynchronizationQueue.Enqueue(new SynchronizationQueueRequest(configuration));
        viewModel.OperationMessage = result.Added && ContentStorageSpaceService is not null
            ? $"{result.Message} O espaço em disco foi verificado."
            : result.Message;
    }

    private async Task<SynchronizationQueueCompletion> ExecuteQueuedSynchronizationAsync(
        SynchronizationQueueRequest request,
        IProgress<SynchronizationQueueProgress> queueProgress,
        CancellationToken cancellationToken)
    {
        if (DiscoveryService is null || ContentCatalog is null) throw new InvalidOperationException("Os serviços de sincronização não estão disponíveis.");
        if (request.LinkedVideo is { } linked)
        {
            if (LinkedVideoService is null) throw new InvalidOperationException("O componente de download por link não está disponível.");
            if (linked.Destination != request.Configuration.Source) throw new InvalidOperationException("O programa do vídeo não corresponde ao pedido da fila.");
            var existing = await ContentCatalog.FindByIdAsync(linked.ItemId, cancellationToken);
            if (existing?.IsReadyOffline == true && existing.LocalPath is { } file && File.Exists(file)) return new SynchronizationQueueCompletion(0);
            var progress = new Progress<DownloadProgress>(update => queueProgress.Report(new SynchronizationQueueProgress(
                $"{linked.Video.Title}: {update.Stage}", update.Percentage, GetSynchronizationStage(update.Stage))));
            var downloaded = await LinkedVideoService.DownloadAsync(linked, progress, cancellationToken);
            await ContentCatalog.UpsertAsync([downloaded], cancellationToken);
            _ = Dispatcher.BeginInvoke(() => (DataContext as HomeViewModel)?.MarkItemAsReady(downloaded));
            return new SynchronizationQueueCompletion(1);
        }
        queueProgress.Report(new SynchronizationQueueProgress("Consultando o site oficial...", Stage: SynchronizationStage.Discovery));
        await DiscoveryService.RefreshAsync(request.Configuration, cancellationToken);
        queueProgress.Report(new SynchronizationQueueProgress("Catálogo atualizado. Preparando downloads...", Stage: SynchronizationStage.Download));
        var downloadProgress = new Progress<DownloadProgress>(progress =>
        {
            queueProgress.Report(new SynchronizationQueueProgress(
                progress.Percentage is { } percentage
                    ? $"{progress.Item.Title}: {progress.Stage} ({percentage:0.0}%)"
                    : $"{progress.Item.Title}: {progress.Stage}",
                progress.Percentage,
                GetSynchronizationStage(progress.Stage)));
            if (progress.Item.SyncState == Sinalo.Domain.SyncState.Ready)
            {
                _ = Dispatcher.BeginInvoke(() => (DataContext as HomeViewModel)?.MarkItemAsReady(progress.Item));
            }
        });

        IReadOnlyList<Sinalo.Domain.ContentItem> synchronized = request.SelectedItemIds is { Count: > 0 } && ManualSynchronizationService is not null
            ? await ManualSynchronizationService.SynchronizeAsync(request.Configuration.Source, request.SelectedItemIds, downloadProgress, cancellationToken)
            : request.Configuration.Source switch
        {
            Sinalo.Domain.ContentSource.Missions when MissionsSynchronizationService is not null => request.Configuration.DownloadSelection is { } missionSelection
                ? await MissionsSynchronizationService.SynchronizeAsync(missionSelection, downloadProgress, cancellationToken)
                : await MissionsSynchronizationService.SynchronizeAsync(downloadProgress, cancellationToken),
            Sinalo.Domain.ContentSource.ProvaiEVede when ProvaiEVedeSynchronizationService is not null => await ProvaiEVedeSynchronizationService.SynchronizeQuarterAsync(downloadProgress, request.Configuration.ResolvedDownloadSelection, cancellationToken),
            Sinalo.Domain.ContentSource.Health when HealthSynchronizationService is not null => await HealthSynchronizationService.SynchronizeAsync(request.Configuration.ResolvedDownloadSelection, downloadProgress, cancellationToken),
            _ => throw new InvalidOperationException("O programa selecionado não está disponível para sincronização.")
        };

        if (ConfigurationService is not null)
        {
            var configurations = await ConfigurationService.LoadSourcesAsync(cancellationToken);
            var items = await LoadCatalogAsync();
            _ = Dispatcher.BeginInvoke(() => ReplaceHomeViewModel(
                configurations,
                items,
                synchronized.Count > 0
                    ? $"Sincronização concluída. {synchronized.Count} vídeo(s) de {request.Configuration.DisplayName} estão prontos offline."
                    : $"Catálogo atualizado. Nenhum vídeo novo de {request.Configuration.DisplayName} estava disponível."));
        }

        return new SynchronizationQueueCompletion(synchronized.Count);
    }

    private void SynchronizationQueue_Changed(SynchronizationQueueSnapshot snapshot)
    {
        Dispatcher.BeginInvoke(() => (DataContext as HomeViewModel)?.UpdateSynchronizationQueue(snapshot));
    }

    private static SynchronizationStage GetSynchronizationStage(string stage) => stage switch
    {
        "Baixando" => SynchronizationStage.Download,
        "Extraindo vídeo" => SynchronizationStage.Extraction,
        "Validando arquivo" => SynchronizationStage.Validation,
        "Disponível offline" => SynchronizationStage.Catalog,
        _ => SynchronizationStage.Download
    };

    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / 1024d / 1024 / 1024:0.0} GB"
        : $"{bytes / 1024d / 1024:0} MB";

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void ViewSynchronizationDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SynchronizationDiagnostic diagnostic }) return;
        new SynchronizationDiagnosticWindow(diagnostic, ThemeService) { Owner = this }.ShowDialog();
    }

    private async Task UpdateAndSynchronizeSourceAsync(Sinalo.Domain.ContentSource source)
    {
        if (await RefreshSourceAsync(source)) await SynchronizeSourceAsync(source);
    }

    private async Task<bool> RefreshSourceAsync(Sinalo.Domain.ContentSource source)
    {
        if (ConfigurationService is null || DiscoveryService is null || ContentCatalog is null) return false;
        var sourceName = GetSourceName(source);
        SetBusy($"Consultando o site oficial de {sourceName}...");
        try
        {
            var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
            await DiscoveryService.RefreshAsync(configuration);
            ReplaceHomeViewModel(await ConfigurationService.LoadSourcesAsync(), await LoadCatalogAsync(), $"Catálogo atualizado. {sourceName} foi identificado.");
            return true;
        }
        catch (HttpRequestException)
        {
            UiDialog.Inform(this, "Não foi possível concluir", $"Não foi possível atualizar {sourceName}. Verifique sua conexão e a URL configurada.");
            return false;
        }
        finally { SetIdle(); }
    }

    private async Task SynchronizeSourceAsync(Sinalo.Domain.ContentSource source)
    {
        if (ContentCatalog is null || ConfigurationService is null) return;
        var sourceName = GetSourceName(source);
        SetBusy($"Sincronizando {sourceName}. Os arquivos são validados antes de ficarem offline...");
        try
        {
            var progress = new Progress<DownloadProgress>(item =>
            {
                if (DataContext is HomeViewModel viewModel) viewModel.ReportDownloadProgress(item);
            });
            IReadOnlyList<Sinalo.Domain.ContentItem> synchronized = [];
            if (source == Sinalo.Domain.ContentSource.Missions && MissionsSynchronizationService is not null)
            {
                var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
                synchronized = configuration.DownloadSelection is { } missionSelection
                    ? await MissionsSynchronizationService.SynchronizeAsync(missionSelection, progress)
                    : await MissionsSynchronizationService.SynchronizeAsync(progress);
            }
            if (source == Sinalo.Domain.ContentSource.ProvaiEVede && ProvaiEVedeSynchronizationService is not null)
            {
                var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
                synchronized = await ProvaiEVedeSynchronizationService.SynchronizeQuarterAsync(progress, configuration.ResolvedDownloadSelection);
            }
            if (source == Sinalo.Domain.ContentSource.Health && HealthSynchronizationService is not null)
            {
                var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
                synchronized = await HealthSynchronizationService.SynchronizeAsync(configuration.ResolvedDownloadSelection, progress);
            }
            var message = synchronized.Count > 0
                ? $"Sincronização concluída. {synchronized.Count} vídeo(s) de {sourceName} estão prontos offline."
                : $"Nenhum vídeo novo de {sourceName} estava disponível para sincronizar.";
            ReplaceHomeViewModel(await ConfigurationService.LoadSourcesAsync(), await LoadCatalogAsync(), message);
        }
        catch (HttpRequestException) { UiDialog.Inform(this, "Não foi possível concluir", $"Não foi possível sincronizar {sourceName}. Verifique sua conexão."); }
        catch (IOException) { UiDialog.Inform(this, "Não foi possível concluir", "Não foi possível gravar o vídeo. Verifique o espaço e a pasta de conteúdo."); }
        finally { SetIdle(); }
    }

    private void SourceFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: string filter }) viewModel.SelectProgram(filter);
    }

    private void TimerWorkspace_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.SelectTimerWorkspace();
    private void WorshipTimerWorkspace_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.SelectWorshipTimerWorkspace();
    private void RaffleWorkspace_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.SelectRaffleWorkspace();

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void RaffleAction_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || sender is not FrameworkElement { Tag: string action }) return;
        try
        {
            if (action == "name") viewModel.Raffle.AddName();
            else if (action == "range") viewModel.Raffle.AddRange();
            else if (action == "start") { viewModel.Raffle.Start(); if (RaffleConfigurationService is not null) await RaffleConfigurationService.SaveAsync(viewModel.Raffle.Configuration); }
            else if (action == "display") viewModel.Raffle.ResetDisplay();
            else if (action == "restart" && UiDialog.Confirm(this, "Reiniciar sorteio?", "Todos os participantes voltarão a estar disponíveis.", "Reiniciar")) viewModel.Raffle.Restart();
            else if (action == "clear" && UiDialog.Confirm(this, "Remover participantes?", "Todos os participantes e o resultado serão removidos.", "Remover")) viewModel.Raffle.Clear();
            viewModel.OperationMessage = viewModel.Raffle.StatusLabel;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException) { viewModel.OperationMessage = exception.Message; }
    }

    private void AvailabilityFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: string filter }) viewModel.SelectedAvailability = filter;
    }

    private void CatalogItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: CatalogCard item }) viewModel.SelectedCatalogItem = item;
    }

    private async void PlaybackScreen_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PlaybackConfigurationService is null || DataContext is not HomeViewModel viewModel || viewModel.SelectedPlaybackScreen is null) return;
        await PlaybackConfigurationService.SaveAsync(new PlaybackConfiguration(viewModel.SelectedPlaybackScreen.ScreenNumber, viewModel.SelectedPlaybackScreen.MonitorKey));
    }

    private async void CatalogItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PlaybackService is null || DataContext is not HomeViewModel viewModel) return;
        var item = (sender as FrameworkElement)?.Tag as CatalogCard ?? viewModel.SelectedCatalogItem;
        if (item is null) return;
        if (viewModel.SelectedPlaybackScreen is null)
        {
            viewModel.OperationMessage = "Nenhuma tela de saída foi encontrada. Conecte ou habilite uma tela no Windows.";
            return;
        }
        if (PresentationOutputService?.IsOpen == true)
        {
            viewModel.OperationMessage = "Feche a tela de apresentação antes de reproduzir um vídeo nesta saída.";
            return;
        }

        PlaybackLaunchOptions launchOptions;
        if (MonitorService is null)
        {
            // Mantém os testes e instalações antigas funcionais, embora o aplicativo normal sempre forneça o monitor completo.
            launchOptions = new PlaybackLaunchOptions(viewModel.SelectedPlaybackScreen.ScreenNumber);
        }
        else
        {
            var output = OutputSelectionResolver.Resolve(
                new PlaybackConfiguration(viewModel.SelectedPlaybackScreen.ScreenNumber, viewModel.SelectedPlaybackScreen.MonitorKey),
                await MonitorService.GetOutputsAsync());
            if (output is null)
            {
                viewModel.OperationMessage = "A tela selecionada não está disponível. Verifique a conexão do monitor.";
                return;
            }

            launchOptions = new PlaybackLaunchOptions(output);
        }

        var result = await PlaybackService.PlayAsync(item.Id, launchOptions);
        viewModel.OperationMessage = result.Message;
        if (result.Started && result.Item is not null) viewModel.MarkItemAsPlayed(result.Item);
    }

    private async void TestPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || MonitorService is null || PresentationOutputService is null) return;
        if (viewModel.SelectedPlaybackScreen is null)
        {
            viewModel.OperationMessage = "Nenhuma tela de saída foi encontrada. Conecte ou habilite uma tela no Windows.";
            return;
        }

        var output = OutputSelectionResolver.Resolve(
            new PlaybackConfiguration(viewModel.SelectedPlaybackScreen.ScreenNumber, viewModel.SelectedPlaybackScreen.MonitorKey),
            await MonitorService.GetOutputsAsync());
        if (output is null)
        {
            viewModel.OperationMessage = "A tela selecionada não está disponível. Verifique a conexão do monitor.";
            return;
        }

        var result = await PresentationOutputService.ShowAsync(
            new PresentationScene("Sinalo", output.DisplayName, "Tela de saída conferida. Abra a apresentação pela ferramenta desejada."),
            output);
        _presentationSceneKind = "conference";
        viewModel.IsPresentationOpen = result.Succeeded;
        viewModel.OperationMessage = result.Message;
    }

    private async void ClosePresentation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || PresentationOutputService is null) return;
        await PresentationOutputService.CloseAsync();
        viewModel.OperationMessage = "Tela de apresentação fechada.";
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void TimerRefresh_Tick(object? sender, EventArgs e)
    {
        if (DataContext is HomeViewModel presentationState) presentationState.IsPresentationOpen = PresentationOutputService?.IsOpen == true;
        if (DataContext is not HomeViewModel viewModel) return;
        viewModel.Timer.Refresh();
        PlayWorshipTimerCues(viewModel.WorshipTimer.Refresh());
        if (viewModel.Raffle.IsAnimating)
        {
            viewModel.Raffle.Tick();
            viewModel.OperationMessage = viewModel.Raffle.StatusLabel;
        }
        if (PresentationOutputService?.IsOpen == true && _presentationSceneKind != "conference")
            await PresentationOutputService.UpdateAsync(_presentationSceneKind == "raffle"
                ? new PresentationScene("Sorteio", viewModel.Raffle.CurrentWinner, viewModel.Raffle.StatusLabel)
                : _presentationSceneKind == "worship"
                    ? CreateWorshipTimerScene(viewModel.WorshipTimer)
                    : CreateTimerScene(viewModel.Timer));
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void TimerStartPause_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        try
        {
            if (!viewModel.Timer.IsRunning) _ = viewModel.Timer.Configuration;
            viewModel.Timer.StartOrPause();
            viewModel.OperationMessage = viewModel.Timer.StateLabel;
            await SaveTimerConfigurationAsync(viewModel.Timer);
        }
        catch (FormatException exception) { viewModel.OperationMessage = exception.Message; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void TimerReset_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel) viewModel.Timer.Reset();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void TimerConfiguration_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        try
        {
            viewModel.Timer.ApplyConfiguration();
            await SaveTimerConfigurationAsync(viewModel.Timer);
        }
        catch (FormatException exception)
        {
            viewModel.OperationMessage = exception.Message;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void TimerConfiguration_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => TimerConfiguration_Changed(sender, e);

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void OpenTimerPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || MonitorService is null || PresentationOutputService is null) return;
        if (viewModel.SelectedPlaybackScreen is null)
        {
            viewModel.OperationMessage = "Nenhuma tela de saída foi encontrada. Conecte ou habilite uma tela no Windows.";
            return;
        }
        var output = OutputSelectionResolver.Resolve(
            new PlaybackConfiguration(viewModel.SelectedPlaybackScreen.ScreenNumber, viewModel.SelectedPlaybackScreen.MonitorKey),
            await MonitorService.GetOutputsAsync());
        if (output is null)
        {
            viewModel.OperationMessage = "A tela selecionada não está disponível. Verifique a conexão do monitor.";
            return;
        }
        var result = await PresentationOutputService.ShowAsync(CreateTimerScene(viewModel.Timer), output);
        _presentationSceneKind = "timer";
        viewModel.IsPresentationOpen = result.Succeeded;
        viewModel.OperationMessage = result.Message;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void CloseTimerPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || PresentationOutputService is null) return;
        await PresentationOutputService.CloseAsync();
        viewModel.OperationMessage = "Tela do cronômetro fechada.";
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async Task SaveTimerConfigurationAsync(TimerViewModel timer)
    {
        if (TimerConfigurationService is not null) await TimerConfigurationService.SaveAsync(timer.Configuration);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static PresentationScene CreateTimerScene(TimerViewModel timer)
    {
        var data = timer.GetPresentationData();
        return new PresentationScene("Cronômetro", data.DisplayTime, data.Status);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void WorshipTimerStartStop_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        try
        {
            var wasRunning = viewModel.WorshipTimer.IsRunning;
            PlayWorshipTimerCues(viewModel.WorshipTimer.StartOrStop());
            if (wasRunning) WorshipTimerAudioPlayer?.Stop();
            viewModel.OperationMessage = viewModel.WorshipTimer.StateLabel;
            _ = SaveWorshipTimerConfigurationAsync(viewModel.WorshipTimer);
        }
        catch (FormatException exception)
        {
            viewModel.OperationMessage = exception.Message;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void WorshipTimerAdjust_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || sender is not FrameworkElement { Tag: string tag } || !int.TryParse(tag, out var minutes)) return;
        PlayWorshipTimerCues(viewModel.WorshipTimer.AdjustMinutes(minutes));
        viewModel.OperationMessage = $"Cronômetro de Culto ajustado em {minutes:+#;-#;0} minuto(s).";
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void StopWorshipTimerAudio_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as HomeViewModel)?.WorshipTimer.StopAudio();
        if (DataContext is HomeViewModel viewModel) viewModel.OperationMessage = "Alerta sonoro interrompido.";
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void PlayWorshipTimerAudio_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        viewModel.WorshipTimer.PlaySelectedAudio();
        _ = SaveWorshipTimerConfigurationAsync(viewModel.WorshipTimer);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void PauseResumeWorshipTimerAudio_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as HomeViewModel)?.WorshipTimer.PauseOrResumeAudio();
    }

    internal void WorshipTimerAudioSeek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not System.Windows.Controls.Slider { IsMouseCaptureWithin: true }) return;
        (DataContext as HomeViewModel)?.WorshipTimer.SeekAudio(e.NewValue);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void WorshipTimerConfiguration_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        try
        {
            if (!viewModel.WorshipTimer.IsConfigurationEditable)
            {
                viewModel.OperationMessage = "Desligue o Cronômetro de Culto antes de alterar horário, duração ou alertas.";
                return;
            }
            viewModel.WorshipTimer.ApplyConfiguration();
            await SaveWorshipTimerConfigurationAsync(viewModel.WorshipTimer);
        }
        catch (FormatException exception)
        {
            viewModel.OperationMessage = exception.Message;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void WorshipTimerConfiguration_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => WorshipTimerConfiguration_Changed(sender, e);

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void WorshipTimerAudioConfiguration_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel) await SaveWorshipTimerConfigurationAsync(viewModel.WorshipTimer);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void WorshipTimerAudioConfiguration_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => WorshipTimerAudioConfiguration_Changed(sender, e);

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void OpenWorshipTimerPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || MonitorService is null || PresentationOutputService is null) return;
        if (viewModel.SelectedPlaybackScreen is null)
        {
            viewModel.OperationMessage = "Nenhuma tela de saída foi encontrada. Conecte ou habilite uma tela no Windows.";
            return;
        }
        var output = OutputSelectionResolver.Resolve(
            new PlaybackConfiguration(viewModel.SelectedPlaybackScreen.ScreenNumber, viewModel.SelectedPlaybackScreen.MonitorKey),
            await MonitorService.GetOutputsAsync());
        if (output is null)
        {
            viewModel.OperationMessage = "A tela selecionada não está disponível. Verifique a conexão do monitor.";
            return;
        }
        var result = await PresentationOutputService.ShowAsync(CreateWorshipTimerScene(viewModel.WorshipTimer), output);
        _presentationSceneKind = "worship";
        viewModel.IsPresentationOpen = result.Succeeded;
        viewModel.OperationMessage = result.Message;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal async void CloseWorshipTimerPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel || PresentationOutputService is null) return;
        await PresentationOutputService.CloseAsync();
        viewModel.OperationMessage = "Tela do Cronômetro de Culto fechada.";
    }

    private void PlayWorshipTimerCues(IEnumerable<WorshipTimerAudioCue> cues)
    {
        if (DataContext is not HomeViewModel viewModel) return;
        foreach (var cue in cues) viewModel.WorshipTimer.PlayAutomaticCue(cue);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async Task SaveWorshipTimerConfigurationAsync(WorshipTimerViewModel timer)
    {
        if (WorshipTimerConfigurationService is not null) await WorshipTimerConfigurationService.SaveAsync(timer.Configuration);
    }

    private static PresentationScene CreateWorshipTimerScene(WorshipTimerViewModel timer)
    {
        var data = timer.GetPresentationData();
        return new PresentationScene("Cronômetro de Culto", data.DisplayTime, data.Status);
    }

    private void AddToSchedule_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.AddSelectedToSchedule();
    private async void DeleteSelectedVideo_Click(object sender, RoutedEventArgs e)
    {
        if (ContentDeletionService is null || DataContext is not HomeViewModel { SelectedCatalogItem: { } selected }) return;
        if (!UiDialog.Confirm(this, "Excluir vídeo do computador?", $"O arquivo de '{selected.Title}' será excluído. Ele poderá ser baixado novamente conforme a configuração do programa.", "Excluir arquivo")) return;

        SetBusy($"Excluindo {selected.Title}...");
        try
        {
            await ContentDeletionService.DeleteAsync(selected.Id);
            if (DataContext is HomeViewModel viewModel)
            {
                viewModel.RemoveCatalogItem(selected.Id);
                viewModel.OperationMessage = $"{selected.Title} foi excluído do computador.";
            }
        }
        catch (IOException)
        {
            UiDialog.Inform(this, "Não foi possível concluir", "Não foi possível excluir o vídeo. Feche o VLC ou outro programa que esteja usando o arquivo e tente novamente.");
        }
        catch (InvalidOperationException exception)
        {
            UiDialog.Inform(this, "Não foi possível concluir", exception.Message);
        }
        finally { SetIdle(); }
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void ToggleSelectedVideoPin_Click(object sender, RoutedEventArgs e)
    {
        if (ContentCatalog is null || DataContext is not HomeViewModel { SelectedCatalogItem: { } selected }) return;
        var item = await ContentCatalog.FindByIdAsync(selected.Id);
        if (item is null) return;
        var updated = item with { IsPinned = !item.IsPinned };
        await ContentCatalog.SetPinnedAsync(updated.Id, updated.IsPinned);
        if (DataContext is HomeViewModel viewModel)
        {
            viewModel.MarkItemPinned(updated);
            viewModel.OperationMessage = updated.IsPinned ? $"{updated.Title} foi fixado e não será removido automaticamente." : $"{updated.Title} poderá ser removido pela limpeza automática quando ficar antigo.";
        }
    }
    internal void RemoveSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: ScheduleCard item }) viewModel.RemoveFromSchedule(item);
    }
    internal void MoveScheduleUp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: ScheduleCard item }) viewModel.MoveScheduleItem(item, -1);
    }
    internal void MoveScheduleDown_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: ScheduleCard item }) viewModel.MoveScheduleItem(item, 1);
    }

    private void SetBusy(string message)
    {
        if (DataContext is HomeViewModel viewModel) { viewModel.IsBusy = true; viewModel.OperationMessage = message; }
    }

    private void SetIdle()
    {
        if (DataContext is HomeViewModel viewModel) viewModel.IsBusy = false;
    }

    private async Task<IReadOnlyList<Sinalo.Domain.ContentItem>> LoadCatalogAsync()
    {
        if (ContentCatalog is null) return [];
        var items = new List<Sinalo.Domain.ContentItem>();
        foreach (var source in Enum.GetValues<Sinalo.Domain.ContentSource>()) items.AddRange(await ContentCatalog.ListBySourceAsync(source));
        return items;
    }

    private void ReplaceHomeViewModel(IReadOnlyList<Sinalo.Application.Configuration.SourceConfiguration> configurations, IReadOnlyList<Sinalo.Domain.ContentItem> items, string message)
    {
        var previous = DataContext as HomeViewModel;
        var viewModel = new HomeViewModel(
            new SaturdayWindowService(), new LocalSinaloPathService(), configurations, items,
            previous?.PlaybackScreens, previous?.SelectedPlaybackScreen?.ScreenNumber,
            timer: previous?.Timer,
            raffle: previous?.Raffle,
            worshipTimer: previous?.WorshipTimer) { OperationMessage = message };
        RestoreFilters(viewModel, previous);
        DataContext = viewModel;
    }

    private static void RestoreFilters(HomeViewModel current, HomeViewModel? previous)
    {
        if (previous is null) return;
        current.SelectedSource = previous.SelectedSource;
        current.SelectedAvailability = previous.SelectedAvailability;
        current.SearchQuery = previous.SearchQuery;
        current.RestoreLinkedVideoState(previous);
        current.RestoreWorkspaceState(previous);
    }

    private static string GetSourceName(Sinalo.Domain.ContentSource source) => source switch
    {
        Sinalo.Domain.ContentSource.Missions => "Informativo das Missões",
        Sinalo.Domain.ContentSource.ProvaiEVede => "Provai e Vede",
        Sinalo.Domain.ContentSource.Health => "Minuto de Saúde",
        _ => source.ToString()
    };

    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
