using Sinalo.App.ViewModels;
using Sinalo.Application.Configuration;
using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;
using System.Reflection;
using System.Threading;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sinalo.Application.Playback;
using Sinalo.Application.Synchronization;
using Sinalo.Application.Updates;
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

                window.Show();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
        var pathService = new LocalSinaloPathService(rootPath: Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N")));
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
        Assert.Equal("Pronto offline", viewModel.CatalogItems.Single(item => item.Title == "ready").Status);
        Assert.Single(viewModel.CatalogItems);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.PreviousSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.CurrentSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.NextSaturday));
        Assert.EndsWith("content", viewModel.ContentPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeWorkflow_ShouldShowAQueuedSynchronizationAndPreventDuplicateSelection()
    {
        var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), FakeConfigurationService.DefaultSources);
        viewModel.SelectedSource = "Minuto de Saúde";

        viewModel.UpdateSynchronizationQueue(new SynchronizationQueueSnapshot(true,
        [new SynchronizationQueueEntry(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", SynchronizationQueueState.Waiting, "Aguardando na fila.", null)]));

        Assert.True(viewModel.IsQueueActive);
        Assert.False(viewModel.CanQueueSelectedSource);
        Assert.Equal("Na fila", Assert.Single(viewModel.SynchronizationQueueItems).State);

        viewModel.UpdateSynchronizationQueue(new SynchronizationQueueSnapshot(false,
        [new SynchronizationQueueEntry(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", SynchronizationQueueState.Completed, "1 vídeo disponível offline.", 100, 1)]));

        Assert.False(viewModel.IsQueueActive);
        Assert.True(viewModel.CanQueueSelectedSource);
        Assert.Equal("Concluída", Assert.Single(viewModel.SynchronizationQueueItems).State);
    }

    [Fact]
    public void HomeWorkflow_ShouldDescribeEveryCatalogAndQueueState()
    {
        var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), FakeConfigurationService.DefaultSources);
        var getCatalogStatus = typeof(HomeViewModel).GetMethod("GetCatalogStatus", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal("Página trimestral identificada", getCatalogStatus.Invoke(null, [CatalogItem("page", [], Sinalo.Domain.SyncState.Pending)]));
        Assert.Equal("Somente online", getCatalogStatus.Invoke(null, [CatalogItem("online", [Asset("online")], Sinalo.Domain.SyncState.OnlineOnly)]));
        Assert.Equal("Pronto offline", getCatalogStatus.Invoke(null, [CatalogItem("ready", [Asset("ready")], Sinalo.Domain.SyncState.Ready)]));
        Assert.Equal("Falhou", getCatalogStatus.Invoke(null, [CatalogItem("failed", [Asset("failed")], Sinalo.Domain.SyncState.Failed)]));
        Assert.Equal("Disponível para sincronizar", getCatalogStatus.Invoke(null, [CatalogItem("pending", [Asset("pending")], Sinalo.Domain.SyncState.Pending)]));

        viewModel.UpdateSynchronizationQueue(new SynchronizationQueueSnapshot(false,
        [
            new(Sinalo.Domain.ContentSource.Missions, "Informativo das Missões", SynchronizationQueueState.Waiting, "Aguardando", null),
            new(Sinalo.Domain.ContentSource.ProvaiEVede, "Provai e Vede", SynchronizationQueueState.Running, "Baixando", 25),
            new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", SynchronizationQueueState.Completed, "Concluído", 100),
            new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", SynchronizationQueueState.Failed, "Falhou", null),
            new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", SynchronizationQueueState.Cancelled, "Cancelado", null),
            new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", (SynchronizationQueueState)99, "Desconhecido", null)
        ]));

        Assert.Equal(["Na fila", "Baixando", "Concluída", "Falhou", "Cancelada", "99"], viewModel.SynchronizationQueueItems.Select(item => item.State));
        Assert.True(viewModel.IsBusy);
        Assert.Equal(25, viewModel.SyncProgressPercent);
    }

    [Fact]
    public void HomeWorkflow_ShouldMoveScheduledVideosAndRefreshThemeOnlyForRelevantPreferences()
    {
        var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), FakeConfigurationService.DefaultSources,
        [
            CatalogItem("first", [Asset("first")], Sinalo.Domain.SyncState.Ready),
            CatalogItem("second", [Asset("second")], Sinalo.Domain.SyncState.Ready)
        ]);
        viewModel.SelectedCatalogItem = viewModel.CatalogItems[0];
        viewModel.AddSelectedToSchedule();
        viewModel.SelectedCatalogItem = viewModel.CatalogItems[1];
        viewModel.AddSelectedToSchedule();

        viewModel.MoveScheduleItem(viewModel.ScheduleItems[1], -1);

        Assert.Equal(["second", "first"], viewModel.ScheduleItems.Select(item => item.Id));
        Assert.True(Sinalo.App.SystemThemeService.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.General));
        Assert.True(Sinalo.App.SystemThemeService.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.Color));
        Assert.False(Sinalo.App.SystemThemeService.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.Keyboard));
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
                    var provaiPrevious = (CheckBox)window.FindName("ProvaiPreviousSaturday");
                    var provaiCurrent = (CheckBox)window.FindName("ProvaiCurrentSaturday");
                    var provaiQuarterly = (CheckBox)window.FindName("ProvaiQuarterly");
                    provaiPrevious.IsChecked = true;
                    Assert.False(provaiQuarterly.IsEnabled);
                    provaiPrevious.IsChecked = false;
                    provaiQuarterly.IsChecked = false;
                    Assert.True(provaiQuarterly.IsChecked);
                    provaiCurrent.IsChecked = true;
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
        Assert.Equal(new Sinalo.Domain.DownloadSelection(false, true, false), service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.ProvaiEVede).DownloadSelection);
    }

    [Fact]
    public void ConfigureSourcesWorkflow_ShouldSaveAndRefreshTheHomeViewModel()
    {
        Exception? exception = null;
        var service = new FakeConfigurationService();
        var thread = new Thread(() =>
        {
            try
            {
                var application = System.Windows.Application.Current ?? new System.Windows.Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                var initialViewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), service.LoadSourcesAsync().Result);
                var window = new Sinalo.App.MainWindow
                {
                    ConfigurationService = service,
                    DataContext = initialViewModel
                };
                window.Show();

                window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
                {
                    var settings = application.Windows.OfType<Sinalo.App.SettingsWindow>().Single();
                    ((TextBox)settings.FindName("MissionsUrl")).Text = "https://missions.example/";
                    ((TextBox)settings.FindName("ProvaiUrl")).Text = "https://provai.example/";
                    ((TextBox)settings.FindName("HealthUrl")).Text = "https://health.example/";
                    typeof(Sinalo.App.SettingsWindow)
                        .GetMethod("Save_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(settings, [settings, new RoutedEventArgs()]);
                });

                typeof(Sinalo.App.MainWindow)
                    .GetMethod("ConfigureSources_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [window, new RoutedEventArgs()]);

                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                var refreshedViewModel = Assert.IsType<HomeViewModel>(window.DataContext);
                Assert.NotSame(initialViewModel, refreshedViewModel);
                Assert.Equal("Fonte configurada", refreshedViewModel.Sources.Single(source => source.Source == Sinalo.Domain.ContentSource.Missions).Status);
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
        Assert.True(service.WasSaved);
    }

    [Fact]
    public void UpdateWorkflow_ShouldDownloadTheAvailableReleaseWithoutBlockingTheLibrary()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), FakeConfigurationService.DefaultSources);
                var window = new Sinalo.App.MainWindow { DataContext = viewModel, ApplicationUpdateService = new SuccessfulUpdateService() };
                window.CheckForUpdateAsync().GetAwaiter().GetResult();
                Assert.True(viewModel.IsUpdateReady);
                Assert.Contains("0.1.5", viewModel.UpdateMessage);
            }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(exception);
    }

    [Fact]
    public void UpdateWorkflow_ShouldKeepTheLibraryUsableWhenTheCheckFails()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), FakeConfigurationService.DefaultSources);
                var window = new Sinalo.App.MainWindow { DataContext = viewModel, ApplicationUpdateService = new FailedUpdateService() };
                window.CheckForUpdateAsync().GetAwaiter().GetResult();
                Assert.Contains("download não foi concluído", viewModel.UpdateMessage);
            }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(exception);
    }

    [Fact]
    public void PlaybackScreenWorkflow_ShouldPersistTheSelectedOutputScreen()
    {
        Exception? exception = null;
        var configuration = new RecordingPlaybackConfigurationService();
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), new FakeConfigurationService().LoadSourcesAsync().Result,
                    playbackScreens: [new PlaybackScreenOption("Tela 1 · Principal", 1), new PlaybackScreenOption("Tela 2", 2)],
                    selectedPlaybackScreenNumber: 2);
                var window = new Sinalo.App.MainWindow
                {
                    DataContext = viewModel,
                    PlaybackConfigurationService = configuration
                };
                var args = new System.Windows.Controls.SelectionChangedEventArgs(
                    System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                    new System.Collections.ArrayList(),
                    new System.Collections.ArrayList());

                typeof(Sinalo.App.MainWindow)
                    .GetMethod("PlaybackScreen_SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [window, args]);

                Assert.Equal(2, configuration.Saved?.FullscreenScreenNumber);
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
                typeof(Sinalo.App.MainWindow).GetMethod("MoveScheduleUp_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [new Button { Tag = viewModel.ScheduleItems[0] }, new RoutedEventArgs()]);
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
                ((HomeViewModel)window.DataContext).SelectedSource = "Informativo das Missões";

                ((Task)typeof(Sinalo.App.MainWindow).GetMethod("RefreshSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Sinalo.Domain.ContentSource.Missions])!).GetAwaiter().GetResult();
                ((Task)typeof(Sinalo.App.MainWindow).GetMethod("SynchronizeSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Sinalo.Domain.ContentSource.Missions])!).GetAwaiter().GetResult();

                Assert.Contains(Sinalo.Domain.ContentSource.Missions, catalog.RequestedSources);
                Assert.Equal("Informativo das Missões", ((HomeViewModel)window.DataContext).SelectedSource);
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
    public void HealthSourceActionWorkflow_ShouldRefreshAndSynchronizeWithoutNetwork()
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
                    HealthSynchronizationService = new HealthSynchronizationService(catalog, new NoOpDownloader(), new SaturdayWindowService(), () => new DateOnly(2026, 8, 8))
                };
                ((HomeViewModel)window.DataContext).SelectedSource = "Minuto de Saúde";

                ((Task)typeof(Sinalo.App.MainWindow).GetMethod("RefreshSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Sinalo.Domain.ContentSource.Health])!).GetAwaiter().GetResult();
                ((Task)typeof(Sinalo.App.MainWindow).GetMethod("SynchronizeSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Sinalo.Domain.ContentSource.Health])!).GetAwaiter().GetResult();

                Assert.Contains(Sinalo.Domain.ContentSource.Health, catalog.RequestedSources);
                Assert.Equal("Minuto de Saúde", ((HomeViewModel)window.DataContext).SelectedSource);
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
    public void SourceActionWorkflow_ShouldQueueAllSourcesWithoutConcurrentSynchronization()
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
                    MissionsSynchronizationService = new MissionsSynchronizationService(catalog, new NoOpDownloader(), new SaturdayWindowService()),
                    ProvaiEVedeSynchronizationService = new ProvaiEVedeSynchronizationService(catalog, new NoOpDownloader(), new SaturdayWindowService()),
                    HealthSynchronizationService = new HealthSynchronizationService(catalog, new NoOpDownloader(), new SaturdayWindowService())
                };
                window.SynchronizationQueue = window.CreateSynchronizationQueue();
                var viewModel = (HomeViewModel)window.DataContext;

                foreach (var sourceName in new[] { "Informativo das Missões", "Provai e Vede", "Minuto de Saúde" })
                {
                    viewModel.SelectedSource = sourceName;
                    typeof(Sinalo.App.MainWindow).GetMethod("UpdateAndSynchronizeSelectedSource_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(window, [window, new RoutedEventArgs()]);
                    window.SynchronizationQueue.WhenIdleAsync().GetAwaiter().GetResult();
                }

                Assert.Equal(
                    [Sinalo.Domain.ContentSource.Missions, Sinalo.Domain.ContentSource.ProvaiEVede, Sinalo.Domain.ContentSource.Health],
                    catalog.RequestedSources.Distinct());
                Assert.False(viewModel.IsQueueActive);
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
        public static IReadOnlyList<SourceConfiguration> DefaultSources { get; } =
        [
            new(Sinalo.Domain.ContentSource.Missions, "Informativo das Missões", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull),
            new(Sinalo.Domain.ContentSource.ProvaiEVede, "Provai e Vede", "", Sinalo.Domain.AvailabilityPolicy.QuarterlyFull),
            new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull)
        ];
        public bool WasSaved { get; private set; }
        public IReadOnlyList<SourceConfiguration> SavedSources { get; private set; } = [];

        public Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceConfiguration>>(
                SavedSources.Count > 0
                    ? SavedSources
                    :
                    DefaultSources);

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
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new PlaybackLaunchResult(true, "VLC", "Vídeo aberto no VLC."));
    }

    private sealed class RecordingPlaybackConfigurationService : IPlaybackConfigurationService
    {
        public PlaybackConfiguration? Saved { get; private set; }
        public Task<PlaybackConfiguration> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PlaybackConfiguration(1));
        public Task SaveAsync(PlaybackConfiguration configuration, CancellationToken cancellationToken = default)
        {
            Saved = configuration;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpDownloader : IContentDownloadService
    {
        public Task<Sinalo.Domain.ContentItem> DownloadAsync(Sinalo.Domain.ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(item);
    }

    private sealed class SuccessfulUpdateService : IApplicationUpdateService
    {
        private static readonly AvailableUpdate Update = new(new Version(0, 1, 5), "Notas", new Uri("https://example.test/setup.exe"), null, "sha256:00");
        public Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default) => Task.FromResult<AvailableUpdate?>(Update);
        public Task<DownloadedUpdate> DownloadAsync(AvailableUpdate update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new UpdateDownloadProgress(50, 100));
            return Task.FromResult(new DownloadedUpdate(update, "C:\\temp\\Sinalo-Setup.exe"));
        }
    }

    private sealed class FailedUpdateService : IApplicationUpdateService
    {
        public Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default) => throw new HttpRequestException();
        public Task<DownloadedUpdate> DownloadAsync(AvailableUpdate update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

}
