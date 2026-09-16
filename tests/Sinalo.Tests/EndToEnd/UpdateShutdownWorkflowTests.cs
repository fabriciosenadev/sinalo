using System.Reflection;
using Sinalo.Application.Monitors;
using Sinalo.Application.Presentation;

namespace Sinalo.Tests.EndToEnd;

public sealed class UpdateShutdownWorkflowTests
{
    [Fact]
    public void PrepareForShutdownAsync_ShouldClosePresentationAndPlaybackRuntime()
    {
        Exception? exception = null;
        var presentation = new Presentation();
        var playback = new PlaybackRuntime();
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.MainWindow
                {
                    PresentationOutputService = presentation,
                    PlaybackRuntime = playback
                };
                var shutdown = (Task)typeof(Sinalo.App.MainWindow)
                    .GetMethod("PrepareForShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, null)!;
                shutdown.GetAwaiter().GetResult();
                Assert.True(presentation.WasClosed);
                Assert.True(playback.WasDisposed);
                window.Close();
            }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private sealed class Presentation : IPresentationOutputService
    {
        public bool IsOpen => !WasClosed;
        public bool WasClosed { get; private set; }
        public Task<PresentationOutputResult> ShowAsync(PresentationScene scene, OutputProfile requestedOutput, CancellationToken cancellationToken = default) => Task.FromResult(new PresentationOutputResult(true, string.Empty));
        public Task UpdateAsync(PresentationScene scene, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) { WasClosed = true; return Task.CompletedTask; }
    }

    private sealed class PlaybackRuntime : IAsyncDisposable
    {
        public bool WasDisposed { get; private set; }
        public ValueTask DisposeAsync() { WasDisposed = true; return ValueTask.CompletedTask; }
    }
}
