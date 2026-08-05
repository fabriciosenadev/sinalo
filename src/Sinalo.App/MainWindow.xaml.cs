using System.Windows;
using System.Net.Http;
using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;
using Sinalo.App.ViewModels;

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
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, SystemThemeService.IsWindowsDarkTheme());
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
        SetBusy("Consultando a página oficial do Provai e Vede...");
        try
        {
            var provaiEVede = (await ConfigurationService.LoadSourcesAsync()).Single(configuration => configuration.Source == Sinalo.Domain.ContentSource.ProvaiEVede);
            await DiscoveryService.RefreshAsync(provaiEVede);

            var catalogItems = new List<Sinalo.Domain.ContentItem>();
            catalogItems.AddRange(await ContentCatalog.ListBySourceAsync(Sinalo.Domain.ContentSource.ProvaiEVede));

            ReplaceHomeViewModel(await ConfigurationService.LoadSourcesAsync(), catalogItems, "Catálogo atualizado. A página trimestral foi identificada.");
        }
        catch (HttpRequestException)
        {
            MessageBox.Show(this, "Não foi possível atualizar o catálogo. Verifique sua conexão e as URLs configuradas.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetIdle(); }
    }

    private async void SynchronizeProvaiEVede_Click(object sender, RoutedEventArgs e)
    {
        if (ProvaiEVedeSynchronizationService is null || ContentCatalog is null || ConfigurationService is null) return;
        SetBusy("Sincronizando Provai e Vede. Os arquivos são validados antes de ficarem offline...");
        try
        {
            var progress = new Progress<DownloadProgress>(item =>
            {
                if (DataContext is HomeViewModel viewModel) viewModel.ReportDownloadProgress(item);
            });
            await ProvaiEVedeSynchronizationService.SynchronizeQuarterAsync(progress);
            ReplaceHomeViewModel(await ConfigurationService.LoadSourcesAsync(), await ContentCatalog.ListBySourceAsync(Sinalo.Domain.ContentSource.ProvaiEVede), "Sincronização concluída. Os vídeos prontos podem ser usados offline.");
        }
        catch (HttpRequestException) { MessageBox.Show(this, "Não foi possível sincronizar o Provai e Vede. Verifique sua conexão.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (IOException) { MessageBox.Show(this, "Não foi possível gravar o vídeo. Verifique o espaço e a pasta de conteúdo.", "Sinalo", MessageBoxButton.OK, MessageBoxImage.Warning); }
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

    private void AddToSchedule_Click(object sender, RoutedEventArgs e) => (DataContext as HomeViewModel)?.AddSelectedToSchedule();
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

    private void ReplaceHomeViewModel(IReadOnlyList<Sinalo.Application.Configuration.SourceConfiguration> configurations, IReadOnlyList<Sinalo.Domain.ContentItem> items, string message)
    {
        var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), configurations, items) { OperationMessage = message };
        DataContext = viewModel;
    }
}
