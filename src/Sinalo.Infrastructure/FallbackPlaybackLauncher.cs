using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class FallbackPlaybackLauncher : IPlaybackLauncher, IPlaybackController, IDisposable
{
    private readonly IPlaybackLauncher _primary;
    private readonly IPlaybackLauncher _fallback;
    private readonly IPlaybackController? _primaryController;
    private readonly IPlaybackController? _fallbackController;
    private IPlaybackController? _active;
    public PlaybackSnapshot Current => _active?.Current ?? PlaybackSnapshot.Idle;
    public event Action<PlaybackSnapshot>? Changed;
    public event Action<PlaybackSnapshot>? Ended;

    public FallbackPlaybackLauncher(IPlaybackLauncher primary, IPlaybackLauncher fallback)
    {
        _primary = primary;
        _fallback = fallback;
        _primaryController = primary as IPlaybackController;
        _fallbackController = fallback as IPlaybackController;
        if (_primaryController is not null) { _primaryController.Changed += PrimaryChanged; _primaryController.Ended += PrimaryEnded; }
        if (_fallbackController is not null) { _fallbackController.Changed += FallbackChanged; _fallbackController.Ended += FallbackEnded; }
    }

    public async Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
    {
        if (_active?.Current.IsActive == true && _active != _primaryController)
            return new(false, Current.Player, "Feche o vídeo no player externo antes de abrir outro pelo Sinalo.", false);
        _active = _primaryController;
        var result = await _primary.LaunchAsync(filePath, options, cancellationToken);
        if (result.Started || !result.AllowsFallback) return result;
        _active = _fallbackController;
        return await _fallback.LaunchAsync(filePath, options, cancellationToken);
    }
    private void PrimaryChanged(PlaybackSnapshot snapshot) { if (_active == _primaryController) Changed?.Invoke(snapshot); }
    private void PrimaryEnded(PlaybackSnapshot snapshot) { if (_active == _primaryController) Ended?.Invoke(snapshot); }
    private void FallbackChanged(PlaybackSnapshot snapshot) { if (_active == _fallbackController) Changed?.Invoke(snapshot); }
    private void FallbackEnded(PlaybackSnapshot snapshot) { if (_active == _fallbackController) Ended?.Invoke(snapshot); }
    public Task<PlaybackCommandResult> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) => _active?.SetPausedAsync(paused, cancellationToken) ?? Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> StopMediaAsync(CancellationToken cancellationToken = default) => _active?.StopMediaAsync(cancellationToken) ?? Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> RestartAsync(CancellationToken cancellationToken = default) => _active?.RestartAsync(cancellationToken) ?? Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> SeekAsync(double seconds, CancellationToken cancellationToken = default) => _active?.SeekAsync(seconds, cancellationToken) ?? Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => _active?.SetVolumeAsync(volume, cancellationToken) ?? Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> SetMutedAsync(bool muted, CancellationToken cancellationToken = default) => _active?.SetMutedAsync(muted, cancellationToken) ?? Task.FromResult(PlaybackCommandResult.Unsupported);
    public void Dispose()
    {
        if (_primaryController is not null) { _primaryController.Changed -= PrimaryChanged; _primaryController.Ended -= PrimaryEnded; }
        if (_fallbackController is not null) { _fallbackController.Changed -= FallbackChanged; _fallbackController.Ended -= FallbackEnded; }
    }
}
