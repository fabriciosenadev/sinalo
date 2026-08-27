using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class FallbackPlaybackLauncher(IPlaybackLauncher primary, IPlaybackLauncher fallback) : IPlaybackLauncher
{
    public async Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
    {
        var result = await primary.LaunchAsync(filePath, options, cancellationToken);
        return result.Started ? result : await fallback.LaunchAsync(filePath, options, cancellationToken);
    }
}
