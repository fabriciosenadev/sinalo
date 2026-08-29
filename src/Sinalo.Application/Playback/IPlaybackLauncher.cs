using Sinalo.Application.Monitors;

namespace Sinalo.Application.Playback;

public sealed record PlaybackLaunchResult(bool Started, string PlayerName, string Message);

public sealed record PlaybackLaunchOptions
{
    public PlaybackLaunchOptions(int fullscreenScreenNumber)
    {
        FullscreenScreenNumber = fullscreenScreenNumber;
    }

    public PlaybackLaunchOptions(OutputProfile output)
    {
        ArgumentNullException.ThrowIfNull(output);
        FullscreenScreenNumber = output.ScreenNumber;
        MonitorKey = output.MonitorKey;
        DisplayName = output.DisplayName;
        BoundsX = output.BoundsX;
        BoundsY = output.BoundsY;
        BoundsWidth = output.BoundsWidth;
        BoundsHeight = output.BoundsHeight;
    }

    public int FullscreenScreenNumber { get; }
    public string? MonitorKey { get; }
    public string? DisplayName { get; }
    public int? BoundsX { get; }
    public int? BoundsY { get; }
    public int? BoundsWidth { get; }
    public int? BoundsHeight { get; }

    // MPV e VLC enumeram telas a partir de zero; o Sinalo as apresenta a partir de um.
    public int PlayerScreenIndex => Math.Max(0, FullscreenScreenNumber - 1);
    public bool HasOutputBounds => BoundsX is not null && BoundsY is not null && BoundsWidth is > 0 && BoundsHeight is > 0;
    public string OutputLabel => string.IsNullOrWhiteSpace(DisplayName) ? $"Tela {FullscreenScreenNumber}" : DisplayName;
}

public interface IPlaybackLauncher
{
    Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default);
}

public interface IPlaybackPreloader
{
    Task WarmAsync(CancellationToken cancellationToken = default);
}
