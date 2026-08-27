using System.Reflection;
using System.Threading;
using System.Windows;
using System.IO;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Monitors;
using Sinalo.Application.Presentation;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.EndToEnd;

public sealed class PresentationWorkflowTests
{
    private static readonly OutputProfile Output = new(@"\\.\DISPLAY2", "Tela 2", 2, 1920, 0, 1920, 1080, false);

    [Fact]
    public void TestPresentation_UsesTheSelectedOutputAndCanBeClosed()
    {
        RunInSta(() =>
        {
            var host = new RecordingHost();
            var service = new PresentationOutputService(new TestMonitorService([Output]), new TestWindowFactory(host));
            var window = CreateMainWindow(service, new TestMonitorService([Output]));

            Invoke(window, "TestPresentation_Click");

            Assert.True(host.IsVisible);
            Assert.Contains("Apresentação aberta", ((HomeViewModel)window.DataContext).OperationMessage);

            Invoke(window, "ClosePresentation_Click");

            Assert.True(host.CloseCalled);
            Assert.Contains("fechada", ((HomeViewModel)window.DataContext).OperationMessage);
            window.Close();
        });
    }

    [Fact]
    public void TestPresentation_InformsWhenTheSelectedMonitorIsUnavailable()
    {
        RunInSta(() =>
        {
            var host = new RecordingHost();
            var service = new PresentationOutputService(new TestMonitorService([]), new TestWindowFactory(host));
            var window = CreateMainWindow(service, new TestMonitorService([]));

            Invoke(window, "TestPresentation_Click");

            Assert.False(host.IsVisible);
            Assert.Contains("não está disponível", ((HomeViewModel)window.DataContext).OperationMessage);
            window.Close();
        });
    }

    private static MainWindow CreateMainWindow(IPresentationOutputService presentationService, IMonitorService monitorService) => new()
    {
        DataContext = new HomeViewModel(
            new SaturdayWindowService(),
            new LocalSinaloPathService(Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"))),
            [],
            playbackScreens: [new PlaybackScreenOption("Tela 2", 2, false, Output.MonitorKey)],
            selectedPlaybackScreenNumber: 2),
        MonitorService = monitorService,
        PresentationOutputService = presentationService
    };

    private static void Invoke(MainWindow window, string method) => typeof(MainWindow)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(window, [window, new RoutedEventArgs()]);

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

    private sealed class TestMonitorService(IReadOnlyList<OutputProfile> outputs) : IMonitorService
    {
        public Task<IReadOnlyList<OutputProfile>> GetOutputsAsync(CancellationToken cancellationToken = default) => Task.FromResult(outputs);
    }

    private sealed class TestWindowFactory(IPresentationWindowHost host) : IPresentationWindowFactory
    {
        public IPresentationWindowHost Create() => host;
    }

    private sealed class RecordingHost : IPresentationWindowHost
    {
        public bool IsVisible { get; private set; }
        public bool CloseCalled { get; private set; }
        public void Display(PresentationScene scene, OutputProfile output) => IsVisible = true;
        public void Close()
        {
            CloseCalled = true;
            IsVisible = false;
        }
    }
}
