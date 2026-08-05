namespace Sinalo.Application.Playback;

public sealed record PlaybackConfiguration(int? FullscreenScreenNumber);

public interface IPlaybackConfigurationService
{
    Task<PlaybackConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PlaybackConfiguration configuration, CancellationToken cancellationToken = default);
}
