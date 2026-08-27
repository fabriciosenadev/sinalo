using System.Windows;
using System.Net.Http;
using Sinalo.App.ViewModels;
using Sinalo.Application.Catalog;
using Sinalo.Application.Appearance;
using Sinalo.Application.Synchronization;
using Sinalo.Application.Playback;
using Sinalo.Infrastructure;

namespace Sinalo.App;

public partial class App : System.Windows.Application
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private SystemThemeService? _themeService;
    private MpvPlaybackLauncher? _mpvPlaybackLauncher;

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
        var playbackScreens = GetPlaybackScreens();
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
        var mainWindow = new MainWindow
        {
            DataContext = new HomeViewModel(new SaturdayWindowService(), pathService, configurations, playbackScreens: playbackScreens, selectedPlaybackScreenNumber: ResolvePlaybackScreenNumber(playbackConfiguration, playbackScreens)),
            ConfigurationService = configurationService,
            ContentPathConfigurationService = pathService,
            ContentPathMigrationService = new LocalContentPathMigrationService(pathService, contentCatalog),
            ApplicationUpdateService = new GitHubApplicationUpdateService(_httpClient, pathService),
            UpdateInstallerLauncher = new WindowsUpdateInstallerLauncher(pathService),
            ThemePreferenceService = configurationService,
            ThemeService = _themeService,
            PlaybackConfigurationService = configurationService,
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

    private static IReadOnlyList<PlaybackScreenOption> GetPlaybackScreens()
    {
        return System.Windows.Forms.Screen.AllScreens
            .Select((screen, index) => new PlaybackScreenOption($"Tela {index + 1}{(screen.Primary ? " · Principal" : string.Empty)}", index + 1, screen.Primary))
            .OrderByDescending(screen => screen.IsPrimary)
            .ToArray();
    }

    private static int ResolvePlaybackScreenNumber(PlaybackConfiguration configuration, IReadOnlyList<PlaybackScreenOption> screens)
    {
        if (configuration.FullscreenScreenNumber is int configured && screens.Any(screen => screen.ScreenNumber == configured)) return configured;
        return screens.First().ScreenNumber;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeService?.Dispose();
        _mpvPlaybackLauncher?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _httpClient.Dispose();
        base.OnExit(e);
    }
}
