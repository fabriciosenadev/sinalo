namespace Sinalo.Application.Playback;

public enum PlaybackState { Idle, Loading, Playing, Paused, Ended, Failed }
public enum PlaybackEndReason { Natural, Stopped, Replaced, Error, ExternalClosed, ApplicationShutdown }
[Flags]
public enum PlaybackCapabilities { None = 0, Pause = 1, Stop = 2, Seek = 4, Volume = 8, Restart = 16, All = Pause | Stop | Seek | Volume | Restart }

public sealed record PlaybackSnapshot(
    Guid SessionId, string? ContentId, string Title, string? FilePath, string Player,
    PlaybackLaunchOptions? Output, PlaybackState State, double PositionSeconds = 0,
    double? DurationSeconds = null, double Volume = 100, bool IsMuted = false,
    PlaybackCapabilities Capabilities = PlaybackCapabilities.None, string? Error = null,
    PlaybackEndReason? EndReason = null)
{
    public static PlaybackSnapshot Idle { get; } = new(Guid.Empty, null, "Nenhum vídeo em reprodução", null, "", null, PlaybackState.Idle);
    public bool IsActive => State is PlaybackState.Loading or PlaybackState.Playing or PlaybackState.Paused;
    public bool Supports(PlaybackCapabilities capability) => (Capabilities & capability) == capability;
}

public sealed record PlaybackCommandResult(bool Succeeded, string? Message = null)
{
    public static PlaybackCommandResult Success { get; } = new(true);
    public static PlaybackCommandResult Unsupported { get; } = new(false, "Este player não oferece esse controle dentro do Sinalo.");
}

public interface IPlaybackController
{
    PlaybackSnapshot Current { get; }
    event Action<PlaybackSnapshot>? Changed;
    event Action<PlaybackSnapshot>? Ended;
    Task<PlaybackCommandResult> SetPausedAsync(bool paused, CancellationToken cancellationToken = default);
    Task<PlaybackCommandResult> StopMediaAsync(CancellationToken cancellationToken = default);
    Task<PlaybackCommandResult> RestartAsync(CancellationToken cancellationToken = default);
    Task<PlaybackCommandResult> SeekAsync(double seconds, CancellationToken cancellationToken = default);
    Task<PlaybackCommandResult> SetVolumeAsync(double volume, CancellationToken cancellationToken = default);
    Task<PlaybackCommandResult> SetMutedAsync(bool muted, CancellationToken cancellationToken = default);
}

public sealed class PlaybackOutputCoordinator(IPlaybackController controller)
{
    public async Task<PlaybackCommandResult> PreparePresentationAsync(PlaybackLaunchOptions output, CancellationToken cancellationToken = default)
    {
        var current = controller.Current;
        if (!current.IsActive || current.Output is null) return PlaybackCommandResult.Success;
        var sameOutput = !string.IsNullOrEmpty(current.Output.MonitorKey) && !string.IsNullOrEmpty(output.MonitorKey)
            ? current.Output.MonitorKey == output.MonitorKey
            : current.Output.FullscreenScreenNumber == output.FullscreenScreenNumber;
        if (!sameOutput) return PlaybackCommandResult.Success;
        if (!current.Supports(PlaybackCapabilities.Stop))
            return new(false, "Feche o vídeo no player externo antes de abrir a apresentação nesta tela.");
        return await controller.StopMediaAsync(cancellationToken);
    }
}
