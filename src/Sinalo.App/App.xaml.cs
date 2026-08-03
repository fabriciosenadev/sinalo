using System.Windows;
using System.Net.Http;
using Sinalo.App.ViewModels;
using Sinalo.Application.Catalog;
using Sinalo.Infrastructure;

namespace Sinalo.App;

public partial class App : System.Windows.Application
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var pathService = new LocalSinaloPathService();
        var database = new SinaloDatabase(pathService);
        await database.InitializeAsync();
        var configurationService = new SqliteConfigurationService(pathService);
        var configurations = await configurationService.LoadSourcesAsync();
        var contentCatalog = new SqliteContentCatalog(pathService);
        var discoveryService = new ContentDiscoveryService(
        [
            new ProvaiEVedeDiscoveryConnector(_httpClient)
        ], contentCatalog);

        var mainWindow = new MainWindow
        {
            DataContext = new HomeViewModel(new SaturdayWindowService(), pathService, configurations),
            ConfigurationService = configurationService,
            DiscoveryService = discoveryService,
            ContentCatalog = contentCatalog
        };

        mainWindow.Show();
    }
}
