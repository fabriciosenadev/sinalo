using System.Windows;
using System.Net.Http;
using Sinalo.App.ViewModels;
using Sinalo.Application.Catalog;
using Sinalo.Application.Appearance;
using Sinalo.Application.Synchronization;
using Sinalo.Application.Playback;
using Sinalo.Application.Monitors;
using Sinalo.Application.Presentation;
using Sinalo.Application.Timer;
using Sinalo.Application.WorshipTimer;
using Sinalo.Application.Raffle;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;
using Sinalo.Application.Library;

namespace Sinalo.App;

public partial class App : System.Windows.Application
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private SystemThemeService? _themeService;
    private MpvPlaybackLauncher? _mpvPlaybackLauncher;
    private IPresentationOutputService? _presentationOutputService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var pathService = new LocalSinaloPathService();
        var database = new SinaloDatabase(pathService);
        await database.InitializeAsync();
        var configurationService = new SqliteConfigurationService(pathService);
        _themeService = new SystemThemeService(this);
        _themeService.Start(await ((IThemePreferenceService)configurationService).LoadAsync());
        var configurations = await configurationService.LoadSourcesAsync();
        var playbackConfiguration = await configurationService.LoadAsync();
        var timerConfiguration = await ((ITimerConfigurationService)configurationService).LoadAsync();
        var timerViewModel = new TimerViewModel(new TimerSession(), timerConfiguration);
        var worshipTimerAudioPlayer = new WorshipTimerAudioPlayer();
        var worshipTimerViewModel = new WorshipTimerViewModel(new WorshipTimerSession(), worshipTimerAudioPlayer, await ((IWorshipTimerConfigurationService)configurationService).LoadAsync());
        var raffleViewModel = new RaffleViewModel(new RaffleSession(), await ((IRaffleConfigurationService)configurationService).LoadAsync());
        var monitorService = new MonitorService();
        var outputs = await monitorService.GetOutputsAsync();
        var selectedOutput = OutputSelectionResolver.Resolve(playbackConfiguration, outputs);
        var playbackScreens = outputs
            .Select(output => new PlaybackScreenOption(output.DisplayName, output.ScreenNumber, output.IsPrimary, output.MonitorKey))
            .ToArray();
        var contentCatalog = new SqliteContentCatalog(pathService);
        var libraryRepository = new SqliteLibraryRepository(pathService);
        var contentOperations = new ContentOperationGate();
        var playbackGate = new PlaybackActivityGate();
        var mediaValidator = new Mp4LibraryValidator(playbackGate);
        var libraryImporter = new LocalLibraryImportService(pathService, libraryRepository, mediaValidator, mediaValidator, contentOperations, playbackGate);
        var libraryFiles = new LocalLibraryFileService(libraryRepository, pathService, mediaValidator, contentOperations, playbackGate);
        var contentCleanupService = new LocalContentCleanupService(contentCatalog, pathService, configurationService, libraryRepository);
        try { await contentCleanupService.CleanIfDueAsync(DateOnly.FromDateTime(DateTime.Today)); }
        catch { /* A limpeza não deve impedir a abertura do Sinalo. */ }
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/140 Safari/537.36");
        var discoveryService = new ContentDiscoveryService(
        [
            new ProvaiEVedeDiscoveryConnector(_httpClient),
            new MissionsDiscoveryConnector(_httpClient),
            new HealthDiscoveryConnector(_httpClient)
        ], contentCatalog);
        var storageSpaceService = new ContentStorageSpaceService(_httpClient, pathService);
        var downloader = new OfficialMediaDownloadService(_httpClient, pathService, storageSpaceService, playbackGate);
        var synchronizationService = new ProvaiEVedeSynchronizationService(contentCatalog, downloader, new SaturdayWindowService(), storageSpaceService: storageSpaceService);
        var missionsSynchronizationService = new MissionsSynchronizationService(contentCatalog, downloader, new SaturdayWindowService(), storageSpaceService: storageSpaceService);

        var mpvPlaybackLauncher = new MpvPlaybackLauncher();
        mpvPlaybackLauncher.PlaybackActivityChanged += playbackGate.SetActive;
        var windowsPlaybackLauncher = new WindowsPlaybackLauncher();
        windowsPlaybackLauncher.PlaybackActivityChanged += playbackGate.SetActive;
        _mpvPlaybackLauncher = mpvPlaybackLauncher;
        var presentationOutputService = new PresentationOutputService(monitorService, new PresentationWindowFactory());
        _presentationOutputService = presentationOutputService;
        MainWindow? mainWindow = null;
        var launcher = new FallbackPlaybackLauncher(mpvPlaybackLauncher, windowsPlaybackLauncher);
        var libraryViewModel = new LibraryViewModel(libraryRepository, libraryImporter, libraryFiles,
            new LibraryPlaybackService(libraryRepository, contentCatalog, launcher, mediaValidator), contentCatalog,
            new LocalContentDeletionService(contentCatalog, pathService, playbackGate, contentOperations, libraryRepository),
            resolveOutput: async () =>
            {
                if (mainWindow?.DataContext is not HomeViewModel { SelectedPlaybackScreen: { } screen }) return null;
                var output = OutputSelectionResolver.Resolve(new PlaybackConfiguration(screen.ScreenNumber, screen.MonitorKey), await monitorService.GetOutputsAsync());
                return output is null ? null : new PlaybackLaunchOptions(output);
            }, presentationOpen: () => presentationOutputService.IsOpen);
        mainWindow = new MainWindow
        {
            DataContext = new HomeViewModel(new SaturdayWindowService(), pathService, configurations, playbackScreens: playbackScreens, selectedPlaybackScreenNumber: selectedOutput?.ScreenNumber, timer: timerViewModel, raffle: raffleViewModel, worshipTimer: worshipTimerViewModel),
            ConfigurationService = configurationService,
            ContentPathConfigurationService = pathService,
            ContentPathMigrationService = new LocalContentPathMigrationService(pathService, contentCatalog, libraryRepository, contentOperations, playbackGate),
            Library = libraryViewModel,
            ContentOperations = contentOperations,
            ApplicationUpdateService = new GitHubApplicationUpdateService(_httpClient, pathService),
            UpdateInstallerLauncher = new WindowsUpdateInstallerLauncher(pathService),
            ThemePreferenceService = configurationService,
            ContentCleanupConfigurationService = configurationService,
            ContentCleanupService = contentCleanupService,
            ThemeService = _themeService,
            PlaybackConfigurationService = configurationService,
            MonitorService = monitorService,
            PresentationOutputService = presentationOutputService,
            WorshipTimerAudioPlayer = worshipTimerAudioPlayer,
            TimerConfigurationService = configurationService,
            WorshipTimerConfigurationService = configurationService,
            RaffleConfigurationService = configurationService,
            DiscoveryService = discoveryService,
            ContentCatalog = contentCatalog,
            ContentDeletionService = new LocalContentDeletionService(contentCatalog, pathService, playbackGate, contentOperations, libraryRepository),
            ContentStorageSpaceService = storageSpaceService,
            SynchronizationDiagnosticStore = new LocalSynchronizationDiagnosticStore(pathService),
            ProvaiEVedeSynchronizationService = synchronizationService,
            MissionsSynchronizationService = missionsSynchronizationService,
            HealthSynchronizationService = new HealthSynchronizationService(contentCatalog, downloader, new SaturdayWindowService(), storageSpaceService: storageSpaceService),
            ManualSynchronizationService = new ManualContentSynchronizationService(contentCatalog, downloader, storageSpaceService),
            LinkedVideoService = new LinkedVideoService(pathService, playbackGate: playbackGate),
            PlaybackService = new PlaybackService(contentCatalog, launcher),
            PlaybackRuntime = mpvPlaybackLauncher
        };

        mainWindow.SynchronizationQueue = mainWindow.CreateSynchronizationQueue();

        mainWindow.Show();
        _ = Task.Run(async () => { try { await libraryImporter.RecoverAsync(); } catch { /* Retry recovery before the next import. */ } });
        _themeService.ApplyCurrentTheme();
        _ = mainWindow.CheckForUpdateAsync();
        mainWindow.StartPeriodicUpdateChecks();
        _ = Task.Run(async () => await mpvPlaybackLauncher.WarmAsync());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _presentationOutputService?.CloseAsync().GetAwaiter().GetResult(); }
        catch { }
        _themeService?.Dispose();
        try { _mpvPlaybackLauncher?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { }
        _httpClient.Dispose();
        base.OnExit(e);
    }
}
