namespace Sinalo.Application.Playback;

public sealed record PlaybackLaunchResult(bool Started, string PlayerName, string Message);
public sealed record PlaybackLaunchOptions(int FullscreenScreenNumber);

public interface IPlaybackLauncher
{
    Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default);
}

public interface IPlaybackPreloader
{
    Task WarmAsync(CancellationToken cancellationToken = default);
}
