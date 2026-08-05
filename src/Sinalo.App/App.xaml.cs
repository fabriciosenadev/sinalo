using System.Windows;
using System.Net.Http;
using Sinalo.App.ViewModels;
using Sinalo.Application.Catalog;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;

namespace Sinalo.App;

public partial class App : System.Windows.Application
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private SystemThemeService? _themeService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _themeService = new SystemThemeService(this);
        _themeService.Start();

        var pathService = new LocalSinaloPathService();
        var database = new SinaloDatabase(pathService);
        await database.InitializeAsync();
        var configurationService = new SqliteConfigurationService(pathService);
        var configurations = await configurationService.LoadSourcesAsync();
        var contentCatalog = new SqliteContentCatalog(pathService);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/140 Safari/537.36");
        var discoveryService = new ContentDiscoveryService(
        [
            new ProvaiEVedeDiscoveryConnector(_httpClient)
        ], contentCatalog);
        var synchronizationService = new ProvaiEVedeSynchronizationService(contentCatalog, new OfficialMediaDownloadService(_httpClient, pathService));

        var mainWindow = new MainWindow
        {
            DataContext = new HomeViewModel(new SaturdayWindowService(), pathService, configurations),
            ConfigurationService = configurationService,
            DiscoveryService = discoveryService,
            ContentCatalog = contentCatalog,
            ProvaiEVedeSynchronizationService = synchronizationService
        };

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeService?.Dispose();
        _httpClient.Dispose();
        base.OnExit(e);
    }
}
