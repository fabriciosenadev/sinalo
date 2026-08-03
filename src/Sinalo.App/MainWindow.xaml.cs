using System.Windows;
using System.Net.Http;
using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;

namespace Sinalo.App;

public partial class MainWindow : Window
{
    public ISinaloConfigurationService? ConfigurationService { get; init; }
    public ContentDiscoveryService? DiscoveryService { get; init; }
    public IContentCatalog? ContentCatalog { get; init; }
    public ProvaiEVedeSynchronizationService? ProvaiEVedeSynchronizationService { get; init; }
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

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigurationService is null || DiscoveryService is null || ContentCatalog is null) return;
        try
        {
            var provaiEVede = (await ConfigurationService.LoadSourcesAsync()).Single(configuration => configuration.Source == Sinalo.Domain.ContentSource.ProvaiEVede);
            await DiscoveryService.RefreshAsync(provaiEVede);

            var catalogItems = new List<Sinalo.Domain.ContentItem>();
            catalogItems.AddRange(await ContentCatalog.ListBySourceAsync(Sinalo.Domain.ContentSource.ProvaiEVede));

            DataContext = new ViewModels.HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), await ConfigurationService.LoadSourcesAsync(), catalogItems);
        }
        catch (HttpRequestException)
        {
            MessageBox.Show(this, "Não foi possível atualizar o catálogo. Verifique sua conexão e as URLs configuradas.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SynchronizeProvaiEVede_Click(object sender, RoutedEventArgs e)
    {
        if (ProvaiEVedeSynchronizationService is null || ContentCatalog is null || ConfigurationService is null) return;
        try
        {
            await ProvaiEVedeSynchronizationService.SynchronizeQuarterAsync();
            DataContext = new ViewModels.HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), await ConfigurationService.LoadSourcesAsync(), await ContentCatalog.ListBySourceAsync(Sinalo.Domain.ContentSource.ProvaiEVede));
        }
        catch (HttpRequestException) { MessageBox.Show(this, "Não foi possível sincronizar o Provai e Vede. Verifique sua conexão.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (IOException) { MessageBox.Show(this, "Não foi possível gravar o vídeo. Verifique o espaço e a pasta de conteúdo.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
