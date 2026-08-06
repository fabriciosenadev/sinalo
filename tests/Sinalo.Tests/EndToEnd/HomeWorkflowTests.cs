using Sinalo.App.ViewModels;
using Sinalo.Application.Configuration;
using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sinalo.Application.Playback;
using Sinalo.Application.Synchronization;
using System.IO;

namespace Sinalo.Tests.EndToEnd;

public sealed class HomeWorkflowTests
{
    [Fact]
    public void MainWindow_ShouldLoadItsVisualTreeOnAnStaThread()
    {
        Exception? exception = null;
        string? title = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.MainWindow();
                title = window.Title;

                var initializeComponent = typeof(Sinalo.App.MainWindow)
                    .GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                initializeComponent!.Invoke(window, null);
                Sinalo.App.SystemThemeService.ApplyTitleBar(window, false);

                window.Close();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
        Assert.Equal("Sinalo", title);
    }

    [Fact]
    public void HomeWorkflow_ShouldExposeOperationalDatesSourcesAndContentLocation()
    {
        var pathService = new LocalSinaloPathService();
        var viewModel = new HomeViewModel(new SaturdayWindowService(), pathService,
        [new(Sinalo.Domain.ContentSource.Missions, "Informativo das Missões", "https://missions.example/", Sinalo.Domain.AvailabilityPolicy.MonthlyFull), new(Sinalo.Domain.ContentSource.ProvaiEVede, "Provai e Vede", "", Sinalo.Domain.AvailabilityPolicy.QuarterlyFull), new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull)],
        [
            CatalogItem("quarter", [], Sinalo.Domain.SyncState.Pending),
            CatalogItem("online", [Asset("online")], Sinalo.Domain.SyncState.OnlineOnly),
            CatalogItem("ready", [Asset("ready")], Sinalo.Domain.SyncState.Ready),
            CatalogItem("pending", [Asset("pending")], Sinalo.Domain.SyncState.Pending)
        ]);

        Assert.Equal(3, viewModel.Sources.Count);
        Assert.Contains("Informativo", viewModel.Sources[0].Name);
        Assert.Equal("Fonte configurada", viewModel.Sources[0].Status);
        Assert.Equal("Configuração da fonte pendente", viewModel.Sources[1].Status);
        Assert.Equal("Página trimestral identificada", viewModel.CatalogItems.Single(item => item.Title == "quarter").Status);
        Assert.Equal("Somente online", viewModel.CatalogItems.Single(item => item.Title == "online").Status);
        Assert.Equal("Pronto offline", viewModel.CatalogItems.Single(item => item.Title == "ready").Status);
        Assert.Equal("Disponível para sincronizar", viewModel.CatalogItems.Single(item => item.Title == "pending").Status);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.PreviousSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.CurrentSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.NextSaturday));
        Assert.EndsWith("Sinalo\\content", viewModel.ContentPath, StringComparison.OrdinalIgnoreCase);
    }

    private static Sinalo.Domain.ContentItem CatalogItem(string title, IReadOnlyList<Sinalo.Domain.MediaAsset> assets, Sinalo.Domain.SyncState syncState) => new(title, Sinalo.Domain.ContentSource.ProvaiEVede, title, new DateOnly(2026, 8, 8), new Uri("https://example.test/" + title), assets, syncState);
    private static Sinalo.Domain.MediaAsset Asset(string id) => new(id, new Uri("https://example.test/" + id + ".mp4"), id + ".mp4", null, null);

    [Fact]
    public void SettingsWorkflow_ShouldLoadAndSaveTheThreeConfiguredUrls()
    {
        Exception? exception = null;
        var service = new FakeConfigurationService();
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.SettingsWindow(service);
                window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(() =>
                {
                    ((TextBox)window.FindName("MissionsUrl")).Text = "https://missions.example/";
                    ((TextBox)window.FindName("ProvaiUrl")).Text = "https://provai.example/";
                    ((TextBox)window.FindName("HealthUrl")).Text = "https://health.example/";
                    typeof(Sinalo.App.SettingsWindow)
                        .GetMethod("Save_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(window, [window, new RoutedEventArgs()]);
                });

                window.ShowDialog();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
        Assert.True(service.WasSaved);
        Assert.Equal("https://missions.example/", service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.Missions).PageUrl);
        Assert.Equal("https://provai.example/", service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.ProvaiEVede).PageUrl);
        Assert.Equal("https://health.example/", service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.Health).PageUrl);
    }

    [Fact]
    public void RefreshCatalogWorkflow_ShouldReplaceTheHomeViewModelWithoutUsingTheNetwork()
    {
        Exception? exception = null;
        object? dataContext = null;
        var configurations = new FakeConfigurationService();
        var catalog = new MemoryCatalog();
        var discovery = new ContentDiscoveryService([], catalog);
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.MainWindow
                {
                    ConfigurationService = configurations,
                    DiscoveryService = discovery,
                    ContentCatalog = catalog
                };
                typeof(Sinalo.App.MainWindow)
                    .GetMethod("RefreshCatalog_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [window, new RoutedEventArgs()]);
                dataContext = window.DataContext;
                window.Close();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
        Assert.IsType<HomeViewModel>(dataContext);
        Assert.Contains(Sinalo.Domain.ContentSource.ProvaiEVede, catalog.RequestedSources);
        Assert.Equal(3, catalog.RequestedSources.Distinct().Count());
    }

    [Fact]
    public void MainWindow_FilterAndScheduleControls_ShouldUpdateTheViewModel()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), new FakeConfigurationService().LoadSourcesAsync().Result,
                [CatalogItem("ready", [Asset("ready")], Sinalo.Domain.SyncState.Ready)]);
                var window = new Sinalo.App.MainWindow { DataContext = viewModel };
                var sourceButton = new Button { Tag = "Provai e Vede" };
                var availabilityButton = new Button { Tag = "Pronto offline" };
                var catalogButton = new Button { Tag = viewModel.CatalogItems[0] };
                typeof(Sinalo.App.MainWindow).GetMethod("SourceFilter_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [sourceButton, new RoutedEventArgs()]);
                typeof(Sinalo.App.MainWindow).GetMethod("AvailabilityFilter_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [availabilityButton, new RoutedEventArgs()]);
                typeof(Sinalo.App.MainWindow).GetMethod("CatalogItem_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [catalogButton, new RoutedEventArgs()]);
                typeof(Sinalo.App.MainWindow).GetMethod("AddToSchedule_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
                typeof(Sinalo.App.MainWindow).GetMethod("MoveScheduleDown_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [new Button { Tag = viewModel.ScheduleItems[0] }, new RoutedEventArgs()]);
                typeof(Sinalo.App.MainWindow).GetMethod("RemoveSchedule_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [new Button { Tag = viewModel.ScheduleItems[0] }, new RoutedEventArgs()]);
                Assert.Equal("Provai e Vede", viewModel.SelectedSource);
                Assert.Equal("Pronto offline", viewModel.SelectedAvailability);
                Assert.Empty(viewModel.ScheduleItems);
                window.Close();
            }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(exception);
    }

    [Fact]
    public void CatalogDoubleClick_ShouldLaunchReadyItemAndUpdateItsVisualHistory()
    {
        Exception? exception = null;
        var root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(root, "video.mp4");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(file, [1]);
        var thread = new Thread(() =>
        {
            try
            {
                var item = new Sinalo.Domain.ContentItem("play", Sinalo.Domain.ContentSource.ProvaiEVede, "Vídeo", new DateOnly(2026, 8, 8), new Uri("https://example.test/play"), [], Sinalo.Domain.SyncState.Ready, LocalPath: file);
                var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), new FakeConfigurationService().LoadSourcesAsync().Result, [item]);
                var catalog = new PlaybackCatalog(item);
                var window = new Sinalo.App.MainWindow { DataContext = viewModel, PlaybackService = new PlaybackService(catalog, new SuccessfulLauncher()) };
                var button = new Button { Tag = viewModel.CatalogItems.Single() };
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left);
                typeof(Sinalo.App.MainWindow).GetMethod("CatalogItem_DoubleClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [button, args]);
                Assert.Equal("Reproduzido 1×", viewModel.CatalogItems.Single().PlaybackLabel);
                Assert.Single(catalog.Played);
                window.Close();
            }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Assert.Null(exception);
    }

    [Fact]
    public void SourceActionWorkflow_ShouldRunTheContextualMissionsActionsWithoutNetwork()
    {
        Exception? exception = null;
        var catalog = new MemoryCatalog();
        var configurations = new FakeConfigurationService();
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.MainWindow
                {
                    DataContext = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), configurations.LoadSourcesAsync().Result),
                    ConfigurationService = configurations,
                    ContentCatalog = catalog,
                    DiscoveryService = new ContentDiscoveryService([], catalog),
                    MissionsSynchronizationService = new MissionsSynchronizationService(catalog, new NoOpDownloader(), new SaturdayWindowService(), () => new DateOnly(2026, 8, 3))
                };

                ((Task)typeof(Sinalo.App.MainWindow).GetMethod("RefreshSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Sinalo.Domain.ContentSource.Missions])!).GetAwaiter().GetResult();
                ((Task)typeof(Sinalo.App.MainWindow).GetMethod("SynchronizeSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Sinalo.Domain.ContentSource.Missions])!).GetAwaiter().GetResult();

                Assert.Contains(Sinalo.Domain.ContentSource.Missions, catalog.RequestedSources);
                window.Close();
            }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }


    private sealed class FakeConfigurationService : ISinaloConfigurationService
    {
        public bool WasSaved { get; private set; }
        public IReadOnlyList<SourceConfiguration> SavedSources { get; private set; } = [];

        public Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceConfiguration>>(
            [
                new(Sinalo.Domain.ContentSource.Missions, "Informativo das Missões", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull),
                new(Sinalo.Domain.ContentSource.ProvaiEVede, "Provai e Vede", "", Sinalo.Domain.AvailabilityPolicy.QuarterlyFull),
                new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull)
            ]);

        public Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources, CancellationToken cancellationToken = default)
        {
            SavedSources = sources;
            WasSaved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryCatalog : IContentCatalog
    {
        public List<Sinalo.Domain.ContentSource> RequestedSources { get; } = [];
        public Task UpsertAsync(IReadOnlyList<Sinalo.Domain.ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Sinalo.Domain.ContentItem>> ListBySourceAsync(Sinalo.Domain.ContentSource source, CancellationToken cancellationToken = default)
        {
            RequestedSources.Add(source);
            return Task.FromResult<IReadOnlyList<Sinalo.Domain.ContentItem>>([]);
        }
    }

    private sealed class PlaybackCatalog(Sinalo.Domain.ContentItem item) : IContentCatalog
    {
        public List<string> Played { get; } = [];
        public Task UpsertAsync(IReadOnlyList<Sinalo.Domain.ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Sinalo.Domain.ContentItem>> ListBySourceAsync(Sinalo.Domain.ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Sinalo.Domain.ContentItem>>([item]);
        public Task<Sinalo.Domain.ContentItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<Sinalo.Domain.ContentItem?>(id == item.Id ? item : null);
        public Task RecordPlaybackAsync(string id, DateTimeOffset playedAtUtc, CancellationToken cancellationToken = default) { Played.Add(id); return Task.CompletedTask; }
    }

    private sealed class SuccessfulLauncher : IPlaybackLauncher
    {
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new PlaybackLaunchResult(true, "VLC", "Vídeo aberto no VLC."));
    }

    private sealed class NoOpDownloader : IContentDownloadService
    {
        public Task<Sinalo.Domain.ContentItem> DownloadAsync(Sinalo.Domain.ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(item);
    }

}
