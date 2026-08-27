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
using Sinalo.Application.Raffle;
using Sinalo.Infrastructure;

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
        var raffleViewModel = new RaffleViewModel(new RaffleSession(), await ((IRaffleConfigurationService)configurationService).LoadAsync());
        var monitorService = new MonitorService();
        var outputs = await monitorService.GetOutputsAsync();
        var selectedOutput = OutputSelectionResolver.Resolve(playbackConfiguration, outputs);
        var playbackScreens = outputs
            .Select(output => new PlaybackScreenOption(output.DisplayName, output.ScreenNumber, output.IsPrimary, output.MonitorKey))
            .ToArray();
        var contentCatalog = new SqliteContentCatalog(pathService);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/140 Safari/537.36");
        var discoveryService = new ContentDiscoveryService(
        [
            new ProvaiEVedeDiscoveryConnector(_httpClient),
            new MissionsDiscoveryConnector(_httpClient),
            new HealthDiscoveryConnector(_httpClient)
        ], contentCatalog);
        var downloader = new OfficialMediaDownloadService(_httpClient, pathService);
        var synchronizationService = new ProvaiEVedeSynchronizationService(contentCatalog, downloader, new SaturdayWindowService());
        var missionsSynchronizationService = new MissionsSynchronizationService(contentCatalog, downloader, new SaturdayWindowService());

        var mpvPlaybackLauncher = new MpvPlaybackLauncher();
        _mpvPlaybackLauncher = mpvPlaybackLauncher;
        var presentationOutputService = new PresentationOutputService(monitorService, new PresentationWindowFactory());
        _presentationOutputService = presentationOutputService;
        var mainWindow = new MainWindow
        {
            DataContext = new HomeViewModel(new SaturdayWindowService(), pathService, configurations, playbackScreens: playbackScreens, selectedPlaybackScreenNumber: selectedOutput?.ScreenNumber, timer: timerViewModel, raffle: raffleViewModel),
            ConfigurationService = configurationService,
            ContentPathConfigurationService = pathService,
            ContentPathMigrationService = new LocalContentPathMigrationService(pathService, contentCatalog),
            ApplicationUpdateService = new GitHubApplicationUpdateService(_httpClient, pathService),
            UpdateInstallerLauncher = new WindowsUpdateInstallerLauncher(pathService),
            ThemePreferenceService = configurationService,
            ThemeService = _themeService,
            PlaybackConfigurationService = configurationService,
            MonitorService = monitorService,
            PresentationOutputService = presentationOutputService,
            TimerConfigurationService = configurationService,
            RaffleConfigurationService = configurationService,
            DiscoveryService = discoveryService,
            ContentCatalog = contentCatalog,
            ContentDeletionService = new LocalContentDeletionService(contentCatalog, pathService),
            ProvaiEVedeSynchronizationService = synchronizationService,
            MissionsSynchronizationService = missionsSynchronizationService,
            HealthSynchronizationService = new HealthSynchronizationService(contentCatalog, downloader, new SaturdayWindowService()),
            PlaybackService = new PlaybackService(contentCatalog, new FallbackPlaybackLauncher(mpvPlaybackLauncher, new WindowsPlaybackLauncher()))
        };

        mainWindow.SynchronizationQueue = mainWindow.CreateSynchronizationQueue();

        mainWindow.Show();
        _themeService.ApplyCurrentTheme();
        _ = mainWindow.CheckForUpdateAsync();
        _ = Task.Run(async () => await mpvPlaybackLauncher.WarmAsync());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _presentationOutputService?.CloseAsync().GetAwaiter().GetResult(); }
        catch { }
        _themeService?.Dispose();
        _mpvPlaybackLauncher?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _httpClient.Dispose();
        base.OnExit(e);
    }
}
