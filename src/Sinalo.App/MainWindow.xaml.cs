using System.Windows;
using Sinalo.Application.Configuration;

namespace Sinalo.App;

public partial class MainWindow : Window
{
    public ISinaloConfigurationService? ConfigurationService { get; init; }
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void ConfigureSources_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigurationService is null) return;
        var window = new SettingsWindow(ConfigurationService) { Owner = this };
        window.ShowDialog();
        if (window.Saved) DataContext = new ViewModels.HomeViewModel(new Infrastructure.SaturdayWindowService(), new Infrastructure.LocalSinaloPathService(), await ConfigurationService.LoadSourcesAsync());
    }
}
