namespace Sinalo.Application.Playback;

public sealed record PlaybackLaunchResult(bool Started, string PlayerName, string Message);
public sealed record PlaybackLaunchOptions(int? FullscreenScreenNumber = null);

public interface IPlaybackLauncher
{
    Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions? options = null, CancellationToken cancellationToken = default);
}
