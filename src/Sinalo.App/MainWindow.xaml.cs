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

namespace Sinalo.App;

public partial class MainWindow : Window
{
    private SynchronizationQueue? _synchronizationQueue;
    public ISinaloConfigurationService? ConfigurationService { get; init; }
    public IContentPathConfigurationService? ContentPathConfigurationService { get; init; }
    public IContentPathMigrationService? ContentPathMigrationService { get; init; }
    public IApplicationUpdateService? ApplicationUpdateService { get; init; }
    public IUpdateInstallerLauncher? UpdateInstallerLauncher { get; init; }
    public IThemePreferenceService? ThemePreferenceService { get; init; }
    public SystemThemeService? ThemeService { get; init; }
    private DownloadedUpdate? _downloadedUpdate;
    public IPlaybackConfigurationService? PlaybackConfigurationService { get; init; }
    public ContentDiscoveryService? DiscoveryService { get; init; }
    public IContentCatalog? ContentCatalog { get; init; }
    public IContentDeletionService? ContentDeletionService { get; init; }
    public ProvaiEVedeSynchronizationService? ProvaiEVedeSynchronizationService { get; init; }
    public MissionsSynchronizationService? MissionsSynchronizationService { get; init; }
    public HealthSynchronizationService? HealthSynchronizationService { get; init; }
    public PlaybackService? PlaybackService { get; init; }
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
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, SystemThemeService.IsWindowsDarkTheme());
    }

    private async void ConfigureSources_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigurationService is null) return;
        var window = new SettingsWindow(ConfigurationService, ContentPathConfigurationService, ContentPathMigrationService, ThemePreferenceService, ThemeService) { Owner = this };
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
                previous?.SelectedPlaybackScreen?.ScreenNumber);
            RestoreFilters(viewModel, previous);
            DataContext = viewModel;
        }
    }

    public async Task CheckForUpdateAsync()
    {
        if (ApplicationUpdateService is null || DataContext is not HomeViewModel viewModel) return;
        try
        {
            var versionText = GetType().Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var update = await ApplicationUpdateService.CheckAsync(Version.Parse(versionText));
            if (update is null) return;
            viewModel.ReportUpdateAvailable(update.Version);
            _downloadedUpdate = await ApplicationUpdateService.DownloadAsync(update, new Progress<Sinalo.Application.Updates.UpdateDownloadProgress>(progress => viewModel.ReportUpdateProgress(progress.Percentage)));
            viewModel.ReportUpdateReady(update.Version);
        }
        catch
        {
            viewModel.ReportUpdateFailure();
        }
    }

    private void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadedUpdate is null || UpdateInstallerLauncher is null) return;
        try
        {
            UpdateInstallerLauncher.Launch(_downloadedUpdate.InstallerPath);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            if (DataContext is HomeViewModel viewModel) viewModel.UpdateMessage = $"Não foi possível iniciar a atualização: {exception.Message}";
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

    private async void CancelSynchronizationQueue_Click(object sender, RoutedEventArgs e)
    {
        SynchronizationQueue?.CancelAll();
        await Task.CompletedTask;
    }

    public SynchronizationQueue CreateSynchronizationQueue() => new(ExecuteQueuedSynchronizationAsync);

    private async Task EnqueueSynchronizationAsync(Sinalo.Domain.ContentSource source)
    {
        if (SynchronizationQueue is null || ConfigurationService is null || DataContext is not HomeViewModel viewModel) return;
        var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
        var result = SynchronizationQueue.Enqueue(new SynchronizationQueueRequest(configuration));
        viewModel.OperationMessage = result.Message;
    }

    private async Task<SynchronizationQueueCompletion> ExecuteQueuedSynchronizationAsync(
        SynchronizationQueueRequest request,
        IProgress<SynchronizationQueueProgress> queueProgress,
        CancellationToken cancellationToken)
    {
        if (DiscoveryService is null || ContentCatalog is null) throw new InvalidOperationException("Os serviços de sincronização não estão disponíveis.");
        queueProgress.Report(new SynchronizationQueueProgress("Consultando a fonte oficial..."));
        await DiscoveryService.RefreshAsync(request.Configuration, cancellationToken);
        queueProgress.Report(new SynchronizationQueueProgress("Catálogo atualizado. Preparando downloads..."));
        var downloadProgress = new Progress<DownloadProgress>(progress =>
        {
            queueProgress.Report(new SynchronizationQueueProgress(
                progress.Percentage is { } percentage
                    ? $"{progress.Item.Title}: {progress.Stage} ({percentage:0.0}%)"
                    : $"{progress.Item.Title}: {progress.Stage}",
                progress.Percentage));
            if (progress.Item.SyncState == Sinalo.Domain.SyncState.Ready)
            {
                _ = Dispatcher.BeginInvoke(() => (DataContext as HomeViewModel)?.MarkItemAsReady(progress.Item));
            }
        });

        IReadOnlyList<Sinalo.Domain.ContentItem> synchronized = request.Configuration.Source switch
        {
            Sinalo.Domain.ContentSource.Missions when MissionsSynchronizationService is not null => request.Configuration.DownloadSelection is { } missionSelection
                ? await MissionsSynchronizationService.SynchronizeAsync(missionSelection, downloadProgress, cancellationToken)
                : await MissionsSynchronizationService.SynchronizeAsync(downloadProgress, cancellationToken),
            Sinalo.Domain.ContentSource.ProvaiEVede when ProvaiEVedeSynchronizationService is not null => await ProvaiEVedeSynchronizationService.SynchronizeQuarterAsync(downloadProgress, request.Configuration.ResolvedDownloadSelection, cancellationToken),
            Sinalo.Domain.ContentSource.Health when HealthSynchronizationService is not null => await HealthSynchronizationService.SynchronizeAsync(request.Configuration.ResolvedDownloadSelection, downloadProgress, cancellationToken),
            _ => throw new InvalidOperationException("A fonte selecionada não está disponível para sincronização.")
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

    private async Task UpdateAndSynchronizeSourceAsync(Sinalo.Domain.ContentSource source)
    {
        if (await RefreshSourceAsync(source)) await SynchronizeSourceAsync(source);
    }

    private async Task<bool> RefreshSourceAsync(Sinalo.Domain.ContentSource source)
    {
        if (ConfigurationService is null || DiscoveryService is null || ContentCatalog is null) return false;
        var sourceName = GetSourceName(source);
        SetBusy($"Consultando a fonte oficial {sourceName}...");
        try
        {
            var configuration = (await ConfigurationService.LoadSourcesAsync()).Single(item => item.Source == source);
            await DiscoveryService.RefreshAsync(configuration);
            ReplaceHomeViewModel(await ConfigurationService.LoadSourcesAsync(), await LoadCatalogAsync(), $"Catálogo atualizado. {sourceName} foi identificado.");
            return true;
        }
        catch (HttpRequestException)
        {
            System.Windows.MessageBox.Show(this, $"Não foi possível atualizar {sourceName}. Verifique sua conexão e a URL configurada.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        catch (HttpRequestException) { System.Windows.MessageBox.Show(this, $"Não foi possível sincronizar {sourceName}. Verifique sua conexão.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (IOException) { System.Windows.MessageBox.Show(this, "Não foi possível gravar o vídeo. Verifique o espaço e a pasta de conteúdo.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { SetIdle(); }
    }

    private void SourceFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: string filter }) viewModel.SelectedSource = filter;
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
        await PlaybackConfigurationService.SaveAsync(new PlaybackConfiguration(viewModel.SelectedPlaybackScreen.ScreenNumber));
    }

    private async void CatalogItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PlaybackService is null || DataContext is not HomeViewModel viewModel || sender is not FrameworkElement { Tag: CatalogCard item }) return;
        if (viewModel.SelectedPlaybackScreen is null)
        {
            viewModel.OperationMessage = "Nenhuma tela de saída foi encontrada. Conecte ou habilite uma tela no Windows.";
            return;
        }

        var result = await PlaybackService.PlayAsync(item.Id, new PlaybackLaunchOptions(viewModel.SelectedPlaybackScreen.ScreenNumber));
        viewModel.OperationMessage = result.Message;
        if (result.Started && result.Item is not null) viewModel.MarkItemAsPlayed(result.Item);
    }

    private void AddToSchedule_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.AddSelectedToSchedule();
    private async void DeleteSelectedVideo_Click(object sender, RoutedEventArgs e)
    {
        if (ContentDeletionService is null || DataContext is not HomeViewModel { SelectedCatalogItem: { } selected }) return;
        var confirmation = System.Windows.MessageBox.Show(this, $"Excluir o vídeo '{selected.Title}' do computador?", "Sinalo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

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
            System.Windows.MessageBox.Show(this, "Não foi possível excluir o vídeo. Feche o VLC ou outro programa que esteja usando o arquivo e tente novamente.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (InvalidOperationException exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetIdle(); }
    }
    private void RemoveSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: ScheduleCard item }) viewModel.RemoveFromSchedule(item);
    }
    private void MoveScheduleUp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && sender is FrameworkElement { Tag: ScheduleCard item }) viewModel.MoveScheduleItem(item, -1);
    }
    private void MoveScheduleDown_Click(object sender, RoutedEventArgs e)
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
        var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), configurations, items, previous?.PlaybackScreens, previous?.SelectedPlaybackScreen?.ScreenNumber) { OperationMessage = message };
        RestoreFilters(viewModel, previous);
        DataContext = viewModel;
    }

    private static void RestoreFilters(HomeViewModel current, HomeViewModel? previous)
    {
        if (previous is null) return;
        current.SelectedSource = previous.SelectedSource;
        current.SelectedAvailability = previous.SelectedAvailability;
        current.SearchQuery = previous.SearchQuery;
    }

    private static string GetSourceName(Sinalo.Domain.ContentSource source) => source switch
    {
        Sinalo.Domain.ContentSource.Missions => "Informativo das Missões",
        Sinalo.Domain.ContentSource.ProvaiEVede => "Provai e Vede",
        Sinalo.Domain.ContentSource.Health => "Minuto de Saúde",
        _ => source.ToString()
    };
}
