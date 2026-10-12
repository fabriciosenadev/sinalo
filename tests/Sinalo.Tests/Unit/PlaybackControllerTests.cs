using Sinalo.Application.Playback;
using Sinalo.Application.Catalog;
using Sinalo.App.ViewModels;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using System.IO;

namespace Sinalo.Tests.Unit;

internal sealed class RecordingPlaybackController : IPlaybackController, IPlaybackLauncher
{
    public PlaybackSnapshot Current { get; private set; } = PlaybackSnapshot.Idle;
    public event Action<PlaybackSnapshot>? Changed;
    public event Action<PlaybackSnapshot>? Ended;
    public PlaybackCommandResult Result { get; set; } = PlaybackCommandResult.Success;
    public PlaybackLaunchResult LaunchResult { get; set; } = new(true, "MPV", "Aberto");
    public List<string> Commands { get; } = [];
    public double Sought { get; private set; }
    public TaskCompletionSource? PendingCommand { get; set; }
    public Exception? CommandException { get; set; }
    public void Update(PlaybackSnapshot snapshot) { Current = snapshot; Changed?.Invoke(snapshot); }
    public void Finish(PlaybackEndReason reason) { Update(Current with { State = PlaybackState.Ended, EndReason = reason }); Ended?.Invoke(Current); }
    public async Task<PlaybackCommandResult> SetPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        Commands.Add("pause");
        if (CommandException is not null) throw CommandException;
        if (PendingCommand is not null) await PendingCommand.Task;
        if (Result.Succeeded) Update(Current with { State = paused ? PlaybackState.Paused : PlaybackState.Playing });
        return Result;
    }
    public Task<PlaybackCommandResult> StopMediaAsync(CancellationToken cancellationToken = default) { Commands.Add("stop"); if (Result.Succeeded) Finish(PlaybackEndReason.Stopped); return Task.FromResult(Result); }
    public Task<PlaybackCommandResult> RestartAsync(CancellationToken cancellationToken = default) { Commands.Add("restart"); if (Result.Succeeded) Update(Current with { State = PlaybackState.Playing, PositionSeconds = 0 }); return Task.FromResult(Result); }
    public Task<PlaybackCommandResult> SeekAsync(double seconds, CancellationToken cancellationToken = default) { Commands.Add("seek"); Sought = seconds; if (Result.Succeeded) Update(Current with { PositionSeconds = seconds }); return Task.FromResult(Result); }
    public Task<PlaybackCommandResult> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) { Commands.Add("volume"); if (Result.Succeeded) Update(Current with { Volume = volume }); return Task.FromResult(Result); }
    public Task<PlaybackCommandResult> SetMutedAsync(bool muted, CancellationToken cancellationToken = default) { Commands.Add("mute"); if (Result.Succeeded) Update(Current with { IsMuted = muted }); return Task.FromResult(Result); }
    public Task<PlaybackLaunchResult> LaunchAsync(string path, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
    {
        Commands.Add("launch");
        if (LaunchResult.Started) Update(new(Guid.NewGuid(), options.ContentId, options.Title ?? "Vídeo", path, LaunchResult.PlayerName, options, PlaybackState.Playing, DurationSeconds: 60, Capabilities: PlaybackCapabilities.All));
        return Task.FromResult(LaunchResult);
    }
}

public sealed class PlaybackControllerTests
{
    internal static PlaybackSnapshot Active(PlaybackCapabilities capabilities = PlaybackCapabilities.All, int screen = 1) =>
        new(Guid.NewGuid(), "video", "Vídeo ativo", "C:\\video.mp4", "MPV", new(screen), PlaybackState.Playing, 12, 60, 40, false, capabilities);

    [Theory]
    [InlineData(PlaybackCapabilities.None, false)]
    [InlineData(PlaybackCapabilities.All, true)]
    public async Task PresentationRequiresStopCapabilityOnSameOutput(PlaybackCapabilities capabilities, bool succeeds)
    {
        var controller = new RecordingPlaybackController();
        controller.Update(Active(capabilities));
        var result = await new PlaybackOutputCoordinator(controller).PreparePresentationAsync(new(1));
        Assert.Equal(succeeds, result.Succeeded);
        if (succeeds) Assert.Equal(PlaybackState.Ended, controller.Current.State);
        else Assert.Empty(controller.Commands);
    }

    [Fact]
    public async Task PresentationDoesNotStopAnotherOutputOrIgnoreStopFailure()
    {
        var controller = new RecordingPlaybackController();
        controller.Update(Active(screen: 2));
        var coordinator = new PlaybackOutputCoordinator(controller);
        Assert.True((await coordinator.PreparePresentationAsync(new(1))).Succeeded);
        Assert.Empty(controller.Commands);
        controller.Result = new(false, "Pipe perdido");
        Assert.False((await coordinator.PreparePresentationAsync(new(2))).Succeeded);
        controller.Finish(PlaybackEndReason.Natural);
        Assert.True((await coordinator.PreparePresentationAsync(new(2))).Succeeded);
    }

    [Fact]
    public async Task PresentationMatchesStableMonitorKey()
    {
        var output = new Sinalo.Application.Monitors.OutputProfile("display", "Tela", 2, 0, 0, 100, 100, true);
        var controller = new RecordingPlaybackController();
        controller.Update(Active() with { Output = new(output) });
        Assert.True((await new PlaybackOutputCoordinator(controller).PreparePresentationAsync(new(output))).Succeeded);
        Assert.Contains("stop", controller.Commands);
    }

    [Fact]
    public async Task FallbackDoesNotLaunchForMediaErrorAndForwardsOnlyActivePlayer()
    {
        var primary = new RecordingPlaybackController { LaunchResult = new(false, "MPV", "Mídia inválida", false) };
        var fallback = new RecordingPlaybackController();
        using var service = new FallbackPlaybackLauncher(primary, fallback);
        Assert.False((await service.LaunchAsync("path", new(1))).Started);
        Assert.Empty(fallback.Commands);
        primary.LaunchResult = new(false, "MPV", "MPV indisponível");
        fallback.LaunchResult = new(true, "VLC", "Externo");
        Assert.True((await service.LaunchAsync("path", new(1))).Started);
        var notifications = 0;
        service.Changed += _ => notifications++;
        primary.Update(Active());
        Assert.Equal(0, notifications);
        fallback.Update(fallback.Current with { Capabilities = PlaybackCapabilities.None });
        Assert.Equal("VLC", service.Current.Player);
        Assert.False((await service.LaunchAsync("another", new(1))).Started);
        fallback.Finish(PlaybackEndReason.ExternalClosed);
        primary.LaunchResult = new(true, "MPV", "Pronto");
        await service.LaunchAsync("path", new(1));
        Assert.True((await service.SetPausedAsync(true)).Succeeded);
        Assert.True((await service.SeekAsync(2)).Succeeded);
        Assert.True((await service.SetVolumeAsync(25)).Succeeded);
        Assert.True((await service.SetMutedAsync(true)).Succeeded);
        Assert.True((await service.RestartAsync()).Succeeded);
        Assert.True((await service.StopMediaAsync()).Succeeded);
    }

    [Fact]
    public async Task ViewModelUsesConfirmedStatesAndControls()
    {
        var controller = new RecordingPlaybackController();
        controller.Update(Active());
        using var model = Model(controller);
        Assert.Equal("Pausar", model.PlayPauseLabel);
        await model.PlayPauseCommand.ExecuteAsync(null);
        Assert.Equal("Continuar", model.PlayPauseLabel);
        Assert.Equal("Pausado", model.Status);
        await model.PlayPauseCommand.ExecuteAsync(null);
        model.BeginSeek();
        model.PositionSeconds = 30;
        controller.Update(controller.Current with { PositionSeconds = 15 });
        Assert.Equal(30, model.PositionSeconds);
        await model.CommitSeekAsync();
        Assert.Equal(30, controller.Sought);
        model.BeginVolume();
        model.Volume = 20;
        controller.Update(controller.Current with { PositionSeconds = 16 });
        Assert.Equal(20, model.Volume);
        await model.CommitVolumeAsync();
        await model.ToggleMuteCommand.ExecuteAsync(null);
        Assert.Equal("Ativar som", model.MuteLabel);
        await model.RestartCommand.ExecuteAsync(null);
        Assert.Contains("00:00", model.TimeLabel);
        await model.StopCommand.ExecuteAsync(null);
        Assert.Equal("Encerrado", model.Status);
        Assert.False(model.CanTransport);
        Assert.Equal("Reproduzir", model.PlayPauseLabel);
    }

    [Fact]
    public async Task ViewModelDisablesUnsupportedAndPendingCommandsAndShowsFailure()
    {
        var controller = new RecordingPlaybackController();
        using var model = Model(controller);
        Assert.False(model.HasSession);
        Assert.False(model.PlayPauseCommand.CanExecute(null));
        controller.Update(Active(PlaybackCapabilities.None));
        Assert.False(model.CanVolume);
        Assert.False(model.StopCommand.CanExecute(null));
        Assert.Contains("externo", model.Message);
        controller.Update(Active() with { State = PlaybackState.Loading, DurationSeconds = null });
        Assert.False(model.CanSeek);
        Assert.Contains("--:--", model.TimeLabel);
        controller.Update(Active());
        controller.PendingCommand = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = model.PlayPauseCommand.ExecuteAsync(null);
        Assert.True(model.IsBusy);
        Assert.False(model.StopCommand.CanExecute(null));
        controller.PendingCommand.SetResult();
        await command;
        controller.Result = new(false, "Comando falhou");
        await model.StopCommand.ExecuteAsync(null);
        Assert.Contains("falhou", model.Message);
        controller.Update(Active() with { State = PlaybackState.Failed, Error = "Conexão perdida" });
        Assert.Equal("Falha", model.Status);
        Assert.Contains("perdida", model.Message);
        model.Dispose();
        controller.Update(Active());
        Assert.Equal("Falha", model.Status);
    }

    [Fact]
    public async Task ControlsWithoutAControllerReportUnsupportedInsteadOfSuccess()
    {
        using var fallback = new FallbackPlaybackLauncher(new LauncherOnly(), new LauncherOnly());
        Assert.Equal(PlaybackSnapshot.Idle, fallback.Current);
        Assert.False((await fallback.SetPausedAsync(true)).Succeeded);
        Assert.False((await fallback.StopMediaAsync()).Succeeded);
        Assert.False((await fallback.RestartAsync()).Succeeded);
        Assert.False((await fallback.SeekAsync(1)).Succeeded);
        Assert.False((await fallback.SetVolumeAsync(1)).Succeeded);
        Assert.False((await fallback.SetMutedAsync(true)).Succeeded);
        Assert.False((await fallback.LaunchAsync("path", new(1))).Started);
        var external = new WindowsPlaybackLauncher();
        Assert.False((await external.SetPausedAsync(true)).Succeeded);
        Assert.False((await external.StopMediaAsync()).Succeeded);
        Assert.False((await external.RestartAsync()).Succeeded);
        Assert.False((await external.SeekAsync(1)).Succeeded);
        Assert.False((await external.SetVolumeAsync(1)).Succeeded);
        Assert.False((await external.SetMutedAsync(true)).Succeeded);
    }

    [Fact]
    public async Task ViewModelHandlesExceptionAndResetsEditsWhenSessionChanges()
    {
        var controller = new RecordingPlaybackController();
        controller.Update(Active());
        using var model = Model(controller);
        model.BeginSeek();
        model.BeginVolume();
        model.PositionSeconds = 55;
        model.Volume = 90;
        controller.Update(Active() with { PositionSeconds = 3700, DurationSeconds = 7200, Volume = 10 });
        Assert.Equal(10, model.Volume);
        Assert.Equal("01:01:40 / 02:00:00", model.TimeLabel);
        controller.CommandException = new IOException("Pipe quebrado");
        await model.PlayPauseCommand.ExecuteAsync(null);
        Assert.False(model.IsBusy);
        Assert.Equal("Pipe quebrado", model.Message);
        controller.Update(PlaybackSnapshot.Idle);
        model.BeginSeek();
        model.BeginVolume();
        await model.CommitSeekAsync();
        await model.CommitVolumeAsync();
        Assert.Equal("", model.PlayerAndOutput);
        Assert.Equal("Parado", model.Status);
        Assert.Single(controller.Commands);
    }

    [Fact]
    public void DisposingViewModelIgnoresAlreadyQueuedNotification()
    {
        var controller = new RecordingPlaybackController();
        Action? queued = null;
        var model = new PlaybackViewModel(controller, new(new EmptyCatalog(), controller), action => queued = action);
        controller.Update(Active());
        model.Dispose();
        queued!();
        Assert.False(model.HasSession);
    }

    [Fact]
    public async Task ReplayRequiresTheCurrentlySelectedMonitorToExist()
    {
        var controller = new RecordingPlaybackController();
        controller.Update(Active() with { State = PlaybackState.Ended });
        using var model = new PlaybackViewModel(controller, new(new EmptyCatalog(), controller), resolveReplayOutput: () => Task.FromResult<PlaybackLaunchOptions?>(null));
        await model.PlayPauseCommand.ExecuteAsync(null);
        Assert.Contains("tela selecionada", model.Message);
        Assert.Empty(controller.Commands);
    }

    private sealed class LauncherOnly : IPlaybackLauncher
    {
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new PlaybackLaunchResult(false, "", "Indisponível"));
    }

    private static PlaybackViewModel Model(RecordingPlaybackController controller) => new(controller, new PlaybackService(new EmptyCatalog(), controller));
    private sealed class EmptyCatalog : IContentCatalog
    {
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>([]);
    }
}
