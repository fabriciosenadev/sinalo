using System.Windows;
using Sinalo.App.ViewModels;
using Sinalo.Infrastructure;

namespace Sinalo.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var pathService = new LocalSinaloPathService();
        var database = new SinaloDatabase(pathService);
        await database.InitializeAsync();
        var configurationService = new SqliteConfigurationService(pathService);
        var configurations = await configurationService.LoadSourcesAsync();

        var mainWindow = new MainWindow
        {
            DataContext = new HomeViewModel(new SaturdayWindowService(), pathService, configurations),
            ConfigurationService = configurationService
        };

        mainWindow.Show();
    }
}
