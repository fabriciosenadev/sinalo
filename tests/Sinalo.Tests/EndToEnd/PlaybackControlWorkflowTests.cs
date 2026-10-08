using System.IO;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Monitors;
using Sinalo.Application.Playback;
using Sinalo.Application.Presentation;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using Sinalo.Tests.Integration;
using Sinalo.Tests.Unit;

namespace Sinalo.Tests.EndToEnd;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class PlaybackControlWorkflowTests
{
    [Fact]
    public async Task OfflinePlaybackControlsAndPresentationShareAConfirmedSession()
    {
        using var video = await PlaybackVideoFixture.CreateAsync();
        var paths = new LocalSinaloPathService(rootPath: Path.GetDirectoryName(video.Path)!);
        await new SinaloDatabase(paths).InitializeAsync();
        var catalog = new SqliteContentCatalog(paths);
        await catalog.UpsertAsync([new("video", ContentSource.Missions, "Vídeo válido", new(2026, 10, 3), new("https://example.test/video"), [], SyncState.Ready, LocalPath: video.Path)]);
        var controller = new RecordingPlaybackController();
        var output = new OutputProfile("display", "Principal", 1, 0, 0, 640, 480, true);
        var host = new Host();
        var presentation = new PresentationOutputService(new Monitors(output), new Factory(host), new PlaybackOutputCoordinator(controller));
        var service = new PlaybackService(catalog, controller, presentation);
        var selectedOutput = output;
        ContentItem? replayed = null;
        using var panel = new PlaybackViewModel(controller, service, resolveReplayOutput: () => Task.FromResult<PlaybackLaunchOptions?>(new(selectedOutput)), replayed: item => replayed = item);
        Assert.True((await service.PlayAsync("video", new(output))).Started);
        Assert.Equal("Vídeo válido", panel.Title);
        await panel.PlayPauseCommand.ExecuteAsync(null);
        await panel.RestartCommand.ExecuteAsync(null);
        Assert.Equal(1, (await catalog.FindByIdAsync("video"))!.PlayCount);
        Assert.True((await presentation.ShowAsync(new("Cronômetro", "00:01:00"), output)).Succeeded);
        Assert.True(host.IsVisible);
        Assert.Equal(PlaybackEndReason.Stopped, controller.Current.EndReason);
        await panel.PlayPauseCommand.ExecuteAsync(null);
        Assert.Contains("Feche", panel.Message);
        Assert.Equal(1, (await catalog.FindByIdAsync("video"))!.PlayCount);
        await presentation.CloseAsync();
        selectedOutput = output with { ScreenNumber = 2, MonitorKey = "second" };
        await panel.PlayPauseCommand.ExecuteAsync(null);
        Assert.Equal(2, (await catalog.FindByIdAsync("video"))!.PlayCount);
        Assert.Equal(2, replayed!.PlayCount);
        Assert.Equal(2, controller.Current.Output!.FullscreenScreenNumber);
        Assert.Equal(PlaybackState.Playing, controller.Current.State);
        await panel.StopCommand.ExecuteAsync(null);
        Assert.False(controller.Current.IsActive);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }

    private sealed class Monitors(OutputProfile output) : IMonitorService
    {
        public Task<IReadOnlyList<OutputProfile>> GetOutputsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OutputProfile>>([output]);
    }
    private sealed class Factory(Host host) : IPresentationWindowFactory { public IPresentationWindowHost Create() => host; }
    private sealed class Host : IPresentationWindowHost
    {
        public bool IsVisible { get; private set; }
        public void Display(PresentationScene scene, OutputProfile output) => IsVisible = true;
        public void Close() => IsVisible = false;
    }
}
