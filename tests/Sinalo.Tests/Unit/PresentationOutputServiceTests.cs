using Sinalo.App;
using Sinalo.Application.Monitors;
using Sinalo.Application.Presentation;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Sinalo.Tests.Unit;

public sealed class PresentationOutputServiceTests
{
    private static readonly OutputProfile Output = new(@"\\.\DISPLAY2", "Tela 2", 2, 1920, 0, 1920, 1080, false);

    [Fact]
    public async Task ShowAsync_UsesTheCurrentOutputAndKeepsThePresentationOpen()
    {
        var monitorService = new RecordingMonitorService([Output]);
        var host = new RecordingPresentationWindowHost();
        var service = new PresentationOutputService(monitorService, new RecordingPresentationWindowFactory(host));

        var result = await service.ShowAsync(new PresentationScene("Sinalo", "Pronto"), Output);

        Assert.True(result.Succeeded);
        Assert.True(service.IsOpen);
        Assert.Equal("Pronto", host.Scene?.MainText);
        Assert.Equal(Output, host.Output);
    }

    [Fact]
    public async Task ShowAsync_FailsWhenTheConfiguredOutputWasDisconnected()
    {
        var host = new RecordingPresentationWindowHost();
        var service = new PresentationOutputService(new RecordingMonitorService([]), new RecordingPresentationWindowFactory(host));

        var result = await service.ShowAsync(new PresentationScene("Sinalo", "Pronto"), Output);

        Assert.False(result.Succeeded);
        Assert.False(service.IsOpen);
        Assert.Null(host.Scene);
    }

    [Fact]
    public async Task CloseAsync_ClosesTheOpenPresentation()
    {
        var host = new RecordingPresentationWindowHost();
        var service = new PresentationOutputService(new RecordingMonitorService([Output]), new RecordingPresentationWindowFactory(host));
        await service.ShowAsync(new PresentationScene("Sinalo", "Pronto"), Output);

        await service.CloseAsync();

        Assert.True(host.CloseCalled);
        Assert.False(service.IsOpen);
    }

    [Fact]
    public async Task ShowAsync_ReusesTheExistingPresentationWindow()
    {
        var host = new RecordingPresentationWindowHost();
        var factory = new RecordingPresentationWindowFactory(host);
        var service = new PresentationOutputService(new RecordingMonitorService([Output]), factory);

        await service.ShowAsync(new PresentationScene("Sinalo", "Primeira tela"), Output);
        await service.ShowAsync(new PresentationScene("Sinalo", "Segunda tela"), Output);

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal("Segunda tela", host.Scene?.MainText);
    }

    [Fact]
    public async Task CloseAsync_IsSafeWhenNoPresentationIsOpen()
    {
        var service = new PresentationOutputService(new RecordingMonitorService([Output]), new RecordingPresentationWindowFactory(new RecordingPresentationWindowHost()));

        await service.CloseAsync();

        Assert.False(service.IsOpen);
    }

    [Fact]
    public void PresentationWindow_DisplaysAndReusesTheFullscreenWindow()
    {
        RunInSta(() =>
        {
            var window = new PresentationWindow();
            window.Display(new PresentationScene("Sinalo", "Primeira tela"), Output);
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.Display(new PresentationScene("Sinalo", "Segunda tela"), Output);

            Assert.True(window.IsVisible);
            window.Close();
        });
    }

    [Fact]
    public void PresentationWindow_HandlesPositioningBeforeItsNativeWindowExists()
    {
        RunInSta(() =>
        {
            var window = new PresentationWindow();
            var position = typeof(PresentationWindow).GetMethod("PositionOnOutput", BindingFlags.Instance | BindingFlags.NonPublic)!;
            position.Invoke(window, []);

            typeof(PresentationWindow).GetField("_output", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, Output);
            position.Invoke(window, []);
            window.Close();
        });
    }

    [Fact]
    public void PresentationWindow_ClosesWhenTheOperatorPressesEscape()
    {
        RunInSta(() =>
        {
            var window = new PresentationWindow();
            window.Display(new PresentationScene("Sinalo", "Pronto"), Output);
            var handler = typeof(PresentationWindow).GetMethod("PresentationWindow_PreviewKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var source = PresentationSource.FromVisual(window)!;

            handler.Invoke(window, [window, CreateKeyEvent(source, System.Windows.Input.Key.Enter)]);
            Assert.True(window.IsVisible);

            handler.Invoke(window, [window, CreateKeyEvent(source, System.Windows.Input.Key.Escape)]);
            Assert.False(window.IsVisible);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(exception);
    }

    private static System.Windows.Input.KeyEventArgs CreateKeyEvent(PresentationSource source, System.Windows.Input.Key key) => new(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, key)
    {
        RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent
    };

    private sealed class RecordingMonitorService(IReadOnlyList<OutputProfile> outputs) : IMonitorService
    {
        public Task<IReadOnlyList<OutputProfile>> GetOutputsAsync(CancellationToken cancellationToken = default) => Task.FromResult(outputs);
    }

    private sealed class RecordingPresentationWindowFactory(IPresentationWindowHost host) : IPresentationWindowFactory
    {
        public int CreateCount { get; private set; }
        public IPresentationWindowHost Create()
        {
            CreateCount++;
            return host;
        }
    }

    private sealed class RecordingPresentationWindowHost : IPresentationWindowHost
    {
        public bool IsVisible { get; private set; }
        public bool CloseCalled { get; private set; }
        public PresentationScene? Scene { get; private set; }
        public OutputProfile? Output { get; private set; }

        public void Display(PresentationScene scene, OutputProfile output)
        {
            Scene = scene;
            Output = output;
            IsVisible = true;
        }

        public void Close()
        {
            CloseCalled = true;
            IsVisible = false;
        }
    }
}
