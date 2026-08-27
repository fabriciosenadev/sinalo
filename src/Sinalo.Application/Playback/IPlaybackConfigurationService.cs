namespace Sinalo.Application.Playback;

// Null indica somente uma instalação legada sem tela salva. A reprodução nunca
// recebe esse valor: a interface resolve a tela principal antes de iniciar o player.
public sealed record PlaybackConfiguration(int? FullscreenScreenNumber, string? FullscreenMonitorKey = null);

public interface IPlaybackConfigurationService
{
    Task<PlaybackConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PlaybackConfiguration configuration, CancellationToken cancellationToken = default);
}
