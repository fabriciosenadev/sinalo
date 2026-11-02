using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.App.Views;
using Sinalo.Application.Configuration;
using Sinalo.Application.LouvorJa;
using Sinalo.Infrastructure;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class LouvorJaOptInTests
{
    private sealed class Store : ILouvorJaConfigurationStore
    {
        public LouvorJaConfiguration Configuration = new(@"C:\Test\Louvor JA.exe");
        public bool FailRead, FailWrite;
        public int Writes;
        public Task<LouvorJaConfiguration?> LoadAsync(CancellationToken token = default)
        { if (FailRead) throw new IOException("Erro isolado de leitura"); return Task.FromResult<LouvorJaConfiguration?>(Configuration); }
        public Task SaveAsync(LouvorJaConfiguration configuration, CancellationToken token = default)
        { Configuration = configuration with { Enabled = Configuration.Enabled }; return Task.CompletedTask; }
        public Task SetEnabledAsync(bool enabled, CancellationToken token = default)
        { if (FailWrite) throw new IOException("Erro isolado de gravação"); Writes++; Configuration = Configuration with { Enabled = enabled }; return Task.CompletedTask; }
    }
    private sealed class Sources : ISinaloConfigurationService
    {
        public Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken token = default) => Task.FromResult(OfficialContentPrograms.All);
        public Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources, CancellationToken token = default) => Task.CompletedTask;
    }
    private sealed class Session : ILouvorJaSession
    {
        public int Starts, Stops, Disposals;
        public bool Wait;
        private CancellationTokenSource _lifetime = new();
        public LouvorJaConnectionSnapshot Current { get; private set; } = new(0, LouvorJaConnectionState.Disconnected, "Desconectado");
        public event Action<LouvorJaConnectionSnapshot>? Changed;
        public bool ObserveContent { get; set; }
        public void Configure(LouvorJaConfiguration configuration) { }
        public void InvalidateConfiguration() { }
        public async Task StartAsync(LouvorJaConfiguration configuration, CancellationToken token = default)
        {
            Starts++;
            if (Wait)
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
            }
            Current = new(Current.Sequence + 1, LouvorJaConnectionState.Connected, "Flute pronto", new("idle", "", "", 0, 0, true, 0));
            Changed?.Invoke(Current);
        }
        public Task ConnectAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task OpenAndConnectAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Disconnect() { }
        public void Stop() { Stops++; _lifetime.Cancel(); }
        public ValueTask DisposeAsync() { Disposals++; Stop(); return ValueTask.CompletedTask; }
        public Task<IReadOnlyList<LouvorJaSong>> SearchAsync(string query, CancellationToken token = default) => throw new InvalidOperationException("No catalog calls expected");
        public Task<LouvorJaState> GetStateAsync(CancellationToken token = default) => Task.FromResult(Current.Presentation!);
        public Task OpenAsync(int id, LouvorJaMode mode, CancellationToken token = default) => throw new InvalidOperationException("Must never open music");
        public Task SendAsync(LouvorJaCommand command, int volume = 50, CancellationToken token = default) => throw new InvalidOperationException("Must never stop audio");
    }
    private static HomeViewModel Home() => new(new SaturdayWindowService(), new LocalSinaloPathService(), []);
    private static async Task UntilAsync(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }
    private static void RunUi(Func<Task> action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); }
                catch (Exception caught) { error = caught; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); Assert.Null(error);
    }

    [Fact]
    public void DisabledPreference_DoesNotConstructRuntime_AndIgnoresHiddenNavigation()
    {
        RunUi(async () =>
        {
            var store = new Store(); var creations = 0;
            var window = new MainWindow { DataContext = Home(), LouvorJaConfigurationStore = store,
                LouvorJaViewFactory = () => { creations++; throw new InvalidOperationException("Disabled must not create a runtime"); } };
            try
            {
                window.Show(); await window.RefreshLouvorJaIntegrationAsync();
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("IntegrationNavigation")).Visibility);
                ((Button)window.FindName("LouvorJaNavigationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(0, creations); Assert.False(((HomeViewModel)window.DataContext).IsLouvorJaWorkspace);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void EnableDisableAndReenable_ApplyWithoutRestart_AndPreserveOtherWorkspaces()
    {
        RunUi(async () =>
        {
            var store = new Store(); var sessions = new List<Session>(); var views = new List<LouvorJaIntegrationView>(); var home = Home();
            var window = new MainWindow { DataContext = home, LouvorJaConfigurationStore = store, LouvorJaViewFactory = () =>
            {
                var session = new Session(); sessions.Add(session);
                var view = new LouvorJaIntegrationView(configurationStore: store, session: session); views.Add(view); return view;
            } };
            try
            {
                window.Show(); await window.RefreshLouvorJaIntegrationAsync();
                await store.SetEnabledAsync(true); await window.RefreshLouvorJaIntegrationAsync();
                await UntilAsync(() => views[0].Model.IsConnected);
                ((Button)window.FindName("LouvorJaNavigationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await UntilAsync(() => home.IsLouvorJaWorkspace);
                await store.SetEnabledAsync(false); await window.RefreshLouvorJaIntegrationAsync();
                Assert.True(home.IsGeneralLibraryWorkspace); Assert.Equal(1, sessions[0].Disposals);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("IntegrationNavigation")).Visibility);
                home.SelectTimerWorkspace();
                await store.SetEnabledAsync(true); await window.RefreshLouvorJaIntegrationAsync();
                await UntilAsync(() => views[1].Model.IsConnected);
                Assert.True(home.IsTimerWorkspace); Assert.NotSame(views[0], views[1]);
                await window.RefreshLouvorJaIntegrationAsync(); Assert.Equal(2, views.Count);
                await store.SetEnabledAsync(false); await window.RefreshLouvorJaIntegrationAsync(); Assert.True(home.IsTimerWorkspace);
                Assert.Equal(1, sessions[1].Disposals);
                Assert.Equal(@"C:\Test\Louvor JA.exe", store.Configuration.ExecutablePath);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void DisableDuringReadiness_CancelsImmediately_AndCorruptionFailsClosed()
    {
        RunUi(async () =>
        {
            var store = new Store { Configuration = new(@"C:\Test\Louvor JA.exe", Enabled: true) };
            var session = new Session { Wait = true };
            var window = new MainWindow { DataContext = Home(), LouvorJaConfigurationStore = store,
                LouvorJaViewFactory = () => new(configurationStore: store, session: session) };
            try
            {
                window.Show(); await window.RefreshLouvorJaIntegrationAsync(); await UntilAsync(() => session.Starts == 1);
                await store.SetEnabledAsync(false);
                await window.RefreshLouvorJaIntegrationAsync().WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Equal(1, session.Disposals); Assert.True(session.Stops > 0);
                store.FailRead = true; await window.RefreshLouvorJaIntegrationAsync();
                Assert.Contains("Configurações", ((HomeViewModel)window.DataContext).OperationMessage);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("IntegrationNavigation")).Visibility);
            }
            finally { window.Close(); }
        });
    }

    private static string Capture(SettingsWindow window) => (string)typeof(SettingsWindow).GetMethod("CaptureSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [])!;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
    private static void Save(SettingsWindow window) => ((Button)window.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void RunSettings(Store store, bool dark, Func<SettingsWindow, Task> action)
    {
        Exception? error = null;
        var window = new SettingsWindow(new Sources(), louvorJaConfiguration: store);
        window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml", UriKind.Relative) });
        SystemThemeService.ApplyToResources(window.Resources, dark);
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(async () =>
        {
            try { await action(window); }
            catch (Exception caught) { error = caught; }
            finally
            {
                if (window.IsVisible)
                {
                    typeof(SettingsWindow).GetField("_initialSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, Capture(window));
                    window.DialogResult = false;
                }
            }
        });
        window.ShowDialog(); Assert.Null(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Settings_ExplicitFluteLabel_ThemeAndSaveAreCorrect(bool dark)
    {
        RunUi(() =>
        {
            var store = new Store();
            RunSettings(store, dark, async window =>
            {
                var checkbox = (CheckBox)window.FindName("LouvorJaFluteEnabled"); await UntilAsync(() => checkbox.IsEnabled);
                Assert.Equal("Habilitar integração com Louvor JA Flute", checkbox.Content);
                Assert.False(checkbox.IsChecked); var before = Capture(window);
                checkbox.IsChecked = true; Assert.NotEqual(before, Capture(window)); Assert.Equal(0, store.Writes);
                ((TabControl)window.FindName("SettingsCategories")).SelectedItem = window.FindName("IntegrationsTab");
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../TestResults/louvor-ja")); Directory.CreateDirectory(output);
                using (var file = File.Create(Path.Combine(output, $"settings-{(dark ? "dark" : "light")}.png"))) encoder.Save(file);
                Save(window); await UntilAsync(() => window.Saved);
            });
            Assert.True(store.Configuration.Enabled); Assert.Equal(1, store.Writes);
            RunSettings(store, dark, async window =>
            {
                var checkbox = (CheckBox)window.FindName("LouvorJaFluteEnabled"); await UntilAsync(() => checkbox.IsEnabled);
                Assert.True(checkbox.IsChecked);
                window.ShowProgramSettings();
                Assert.Equal(Visibility.Collapsed, ((TabItem)window.FindName("IntegrationsTab")).Visibility);
            });
            Assert.Equal(1, store.Writes); return Task.CompletedTask;
        });
    }

    [Fact]
    public void Settings_FailedReadCanRetry_AndFailedSaveDoesNotPersist()
    {
        RunUi(() =>
        {
            var store = new Store { FailRead = true };
            RunSettings(store, false, async window =>
            {
                var checkbox = (CheckBox)window.FindName("LouvorJaFluteEnabled");
                Assert.False(checkbox.IsEnabled); Assert.False(checkbox.IsChecked);
                Assert.Contains("não foi substituído", ((TextBlock)window.FindName("LouvorJaSettingsFeedback")).Text);
                store.FailRead = false;
                ((Button)window.FindName("ReloadLouvorJaSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await UntilAsync(() => checkbox.IsEnabled);
                checkbox.IsChecked = true; store.FailWrite = true; Save(window);
                await UntilAsync(() => ((TextBlock)window.FindName("SettingsFeedback")).Text.Contains("Não foi possível"));
                Assert.False(window.Saved); Assert.False(store.Configuration.Enabled); Assert.Equal(0, store.Writes);
                store.FailWrite = false; Save(window); await UntilAsync(() => window.Saved);
            });
            Assert.True(store.Configuration.Enabled); return Task.CompletedTask;
        });
    }

    [Fact]
    public void Settings_DiscardConfirmation_DoesNotApplyCheckbox()
    {
        RunUi(() =>
        {
            var store = new Store();
            RunSettings(store, false, async window =>
            {
                var checkbox = (CheckBox)window.FindName("LouvorJaFluteEnabled"); await UntilAsync(() => checkbox.IsEnabled);
                checkbox.IsChecked = true;
                Exception? promptError = null;
                _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    var prompt = window.OwnedWindows.OfType<Window>().Single();
                    try
                    {
                        prompt.UpdateLayout();
                        Descendants(prompt).OfType<Button>().Single(button => Equals(button.Content, "Descartar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception error) { promptError = error; prompt.DialogResult = false; }
                });
                window.Close(); Assert.Null(promptError);
            });
            Assert.Equal(0, store.Writes); Assert.False(store.Configuration.Enabled); return Task.CompletedTask;
        });
    }

    [Fact]
    public void MainWindow_SettingsSaveDisablesRuntime_BeforeRestoringWorkspace()
    {
        RunUi(async () =>
        {
            var store = new Store { Configuration = new(@"C:\Test\Louvor JA.exe", Enabled: true) };
            var session = new Session(); var home = Home();
            var window = new MainWindow { ConfigurationService = new Sources(), DataContext = home, LouvorJaConfigurationStore = store,
                LouvorJaViewFactory = () => new(configurationStore: store, session: session) };
            try
            {
                window.Show(); await window.RefreshLouvorJaIntegrationAsync(); await UntilAsync(() => session.Starts == 1);
                ((Button)window.FindName("LouvorJaNavigationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(home.IsLouvorJaWorkspace);
                Exception? settingsError = null;
                _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Background, async () =>
                {
                    var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
                    try
                    {
                        var checkbox = (CheckBox)settings.FindName("LouvorJaFluteEnabled"); await UntilAsync(() => checkbox.IsEnabled);
                        checkbox.IsChecked = false; Save(settings);
                    }
                    catch (Exception error)
                    {
                        settingsError = error;
                        typeof(SettingsWindow).GetField("_initialSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(settings, Capture(settings));
                        settings.DialogResult = false;
                    }
                });
                typeof(MainWindow).GetMethod("ConfigureSources_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
                Assert.Null(settingsError);
                await UntilAsync(() => session.Disposals == 1);
                Assert.True(((HomeViewModel)window.DataContext).IsGeneralLibraryWorkspace);
                Assert.False(((HomeViewModel)window.DataContext).IsLouvorJaWorkspace);
                Assert.False(store.Configuration.Enabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RapidToggleAndWindowClose_CancelWaitingRuntimeWithoutReusingDisposedSession()
    {
        RunUi(async () =>
        {
            var store = new Store(); var sessions = new List<Session>();
            var window = new MainWindow { DataContext = Home(), LouvorJaConfigurationStore = store, LouvorJaViewFactory = () =>
            { var session = new Session { Wait = true }; sessions.Add(session); return new(configurationStore: store, session: session); } };
            try
            {
                window.Show(); await window.RefreshLouvorJaIntegrationAsync();
                for (var i = 0; i < 3; i++)
                {
                    await store.SetEnabledAsync(true); await window.RefreshLouvorJaIntegrationAsync();
                    await UntilAsync(() => sessions[^1].Starts == 1);
                    await store.SetEnabledAsync(false); await window.RefreshLouvorJaIntegrationAsync();
                    Assert.Equal(1, sessions[^1].Disposals);
                }
                await store.SetEnabledAsync(true); await window.RefreshLouvorJaIntegrationAsync();
                await UntilAsync(() => sessions[^1].Starts == 1); window.Close();
                Assert.Equal(4, sessions.Count); Assert.All(sessions, session => Assert.Equal(1, session.Disposals));
            }
            finally { if (window.IsVisible) window.Close(); }
        });
    }
}
