using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class LouvorJaExperimentTests
{
    [Fact]
    public void ProductionBuild_IncludesOptInIntegration()
    {
        Assert.NotNull(typeof(HomeViewModel).Assembly.GetType("Sinalo.App.Views.LouvorJaIntegrationView"));
        Assert.False(new Sinalo.Application.LouvorJa.LouvorJaConfiguration("test").Enabled);
    }

    private sealed class ObservedProcess : Sinalo.Application.LouvorJa.ILouvorJaProcessMonitor
    {
        public volatile Sinalo.Application.LouvorJa.LouvorJaProcessObservation Observation = new(Sinalo.Application.LouvorJa.LouvorJaProcessState.Running, 20);
        public Task<Sinalo.Application.LouvorJa.LouvorJaProcessObservation> InspectAsync(string executable, CancellationToken token = default) => Task.FromResult(Observation);
        public Task StartAsync(string executable, CancellationToken token = default) => throw new InvalidOperationException("This UI test must never launch an application.");
    }
    [Fact]
    public void Session_UpdatesWpfBindings_AfterNavigatingAway_AndManualDisconnectPersists()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var process = new ObservedProcess();
            using var http = new System.Net.Http.HttpClient(new Sinalo.Infrastructure.LouvorJa.LouvorJaMockHandler { Latency = TimeSpan.Zero })
                { BaseAddress = new Uri("http://louvorja-simulator.invalid/") };
            var session = new Sinalo.Application.LouvorJa.LouvorJaSession(process, _ => new Sinalo.Infrastructure.LouvorJa.LouvorJaHttpClient(http))
                { PollInterval = TimeSpan.FromMilliseconds(20), ReadinessTimeout = TimeSpan.FromSeconds(2) };
            using var view = new Sinalo.App.Views.LouvorJaIntegrationView(configurationStore: new EmptyConfiguration(), session: session);
            var window = new Window { Width = 900, Height = 740, Content = view };
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml", UriKind.Relative) });
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, async () =>
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    async Task UntilAsync(Func<bool> condition) { while (!condition()) await Task.Delay(10, deadline.Token); }
                    await view.InitializeSessionAsync();
                    Assert.True(view.Model.IsConnected);
                    view.Visibility = Visibility.Collapsed;
                    process.Observation = new(Sinalo.Application.LouvorJa.LouvorJaProcessState.Closed);
                    await UntilAsync(() => !view.Model.IsConnected);
                    Assert.Null(view.Model.State); Assert.False(view.Model.CanControl);
                    view.Visibility = Visibility.Visible;
                    await UntilAsync(() => view.Model.ConnectionLabel.Contains("fechado"));
                    view.Model.DisconnectCommand.Execute(null);
                    process.Observation = new(Sinalo.Application.LouvorJa.LouvorJaProcessState.Running, 30);
                    await Task.Delay(80, deadline.Token);
                    Assert.False(view.Model.IsConnected); Assert.Contains("operador", view.Model.ConnectionLabel);
                    await view.Model.LaunchCommand.ExecuteAsync(null);
                    Assert.True(view.Model.IsConnected);
                    await view.DisposeAsync();
                }
                catch (Exception caught) { error = caught; }
                finally { window.Close(); }
            });
            window.ShowDialog();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); Assert.Null(error);
    }

    private sealed class EmptyConfiguration : Sinalo.Application.LouvorJa.ILouvorJaConfigurationStore
    {
        public Task<Sinalo.Application.LouvorJa.LouvorJaConfiguration?> LoadAsync(CancellationToken token = default) => Task.FromResult<Sinalo.Application.LouvorJa.LouvorJaConfiguration?>(null);
        public Task SaveAsync(Sinalo.Application.LouvorJa.LouvorJaConfiguration configuration, CancellationToken token = default) => Task.CompletedTask;
        public Task SetEnabledAsync(bool enabled, CancellationToken token = default) => Task.CompletedTask;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultSurface_IsRealButDoesNotConnectOrLaunchAutomatically(bool dark)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var view = new Sinalo.App.Views.LouvorJaIntegrationView(configurationStore: new EmptyConfiguration());
                var window = new Window { Width = 900, Height = 740, Content = view };
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml", UriKind.Relative) });
                SystemThemeService.ApplyToResources(window.Resources, dark);
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                {
                    try
                    {
                        window.UpdateLayout();
                        Assert.True(view.Model.IsReal); Assert.False(view.Model.IsConnected); Assert.False(view.Model.IsBusy);
                        Assert.Empty(view.Model.Diagnostics); Assert.Empty(view.Model.Songs);
                        Assert.Equal("Tentar conectar novamente", ((Button)view.FindName("ConnectButton")).Content);
                        Assert.False(((ComboBox)view.FindName("ScenarioSelector")).IsVisible);
                        var output = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../TestResults/louvor-ja"));
                        System.IO.Directory.CreateDirectory(output);
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = System.IO.File.Create(System.IO.Path.Combine(output, $"real-{(dark ? "dark" : "light")}.png")); encoder.Save(stream);
                    }
                    catch (Exception caught) { error = caught; }
                    finally { window.Close(); }
                });
                window.ShowDialog();
            }
            catch (Exception caught) { error = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(error);
    }

    [Fact]
    public void Workspace_LeavesForAllOtherDestinationsAndRestoresAfterRefresh()
    {
        var vm = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), []);
        Action[] navigate = [() => vm.SelectProgram("Todos"), vm.SelectGeneralLibraryWorkspace,
            vm.SelectTimerWorkspace, vm.SelectWorshipTimerWorkspace, vm.SelectRaffleWorkspace, vm.SelectLinkedVideoWorkspace];
        foreach (var destination in navigate)
        {
            vm.SelectLouvorJaWorkspace(); Assert.False(vm.IsLibraryWorkspace); Assert.True(vm.IsLouvorJaWorkspace);
            destination(); Assert.False(vm.IsLouvorJaWorkspace);
        }
        vm.SelectLouvorJaWorkspace();
        var restored = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), []);
        restored.RestoreWorkspaceState(vm);
        Assert.True(restored.IsLouvorJaWorkspace); Assert.False(restored.ShowProgramCalendar);
    }

    [Theory]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    public void OperatorSurface_UsesThemeAndShowsBusyThenConfirmedState(bool dark, int width)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            using var view = new Sinalo.App.Views.LouvorJaIntegrationView(useSimulator: true);
            var window = new Window { Width = width, Height = 740, Content = view, Title = "Teste isolado Louvor JA" };
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml", UriKind.Relative) });
            SystemThemeService.ApplyToResources(window.Resources, dark);
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, async () =>
            {
                try
                {
                    var model = view.Model;
                    window.UpdateLayout();
                    Assert.False(((Button)view.FindName("OpenButton")).IsEnabled);
                    Assert.NotNull(((ComboBox)view.FindName("ScenarioSelector")).ItemTemplate);
                    var operation = model.ConnectCommand.ExecuteAsync(null);
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.True(model.IsBusy);
                    Assert.True(((ProgressBar)view.FindName("OperationProgress")).IsIndeterminate);
                    Assert.False(((ComboBox)view.FindName("ScenarioSelector")).IsEnabled);
                    await operation;
                    var input = (System.Windows.Controls.TextBox)view.FindName("SearchInput");
                    input.Text = "inexistente";
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.True(model.IsSearching); Assert.True(input.IsEnabled);
                    using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        while (model.IsSearching) await Task.Delay(10, deadline.Token);
                    Assert.Empty(model.Songs); Assert.Contains("Nenhuma música", model.SearchStatus);
                    input.Text = "teste";
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    await model.SearchCommand.ExecuteAsync(null);
                    model.SelectedSong = model.Songs[0];
                    await model.OpenCommand.ExecuteAsync(null);
                    window.UpdateLayout();
                    Assert.True(model.CanControl);
                    Assert.Equal(3, ((ListBox)view.FindName("SongList")).Items.Count);
                    Assert.Contains("observado", model.Message);
                    Assert.False(((ProgressBar)view.FindName("OperationProgress")).IsVisible);
                    var output = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../TestResults/louvor-ja"));
                    System.IO.Directory.CreateDirectory(output);
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(output, $"{(dark ? "dark" : "light")}-{width}.png"));
                    encoder.Save(stream);
                    model.ResetCommand.Execute(null);
                    Assert.False(model.CanControl);
                }
                catch (Exception caught) { error = caught; }
                finally { window.Close(); }
            });
            window.ShowDialog();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(error);
    }
}
