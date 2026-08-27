using Sinalo.Infrastructure;
using Sinalo.Application.Playback;
using System.IO;

namespace Sinalo.Tests.Unit;

public sealed class WindowsPlaybackLauncherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));
    private readonly string? _originalPath = Environment.GetEnvironmentVariable("PATH");

    [Theory]
    [InlineData(null, true, true, "Aplicativo padrão")]
    [InlineData("C:\\VideoLAN\\vlc.exe", true, true, "VLC")]
    [InlineData("C:\\VideoLAN\\vlc.exe", false, false, "")]
    public void CreateLaunchResult_ShouldDescribeVlcFallbackAndFailure(string? vlcPath, bool started, bool expectedStarted, string player)
    {
        var result = WindowsPlaybackLauncher.CreateLaunchResult(vlcPath, started);
        Assert.Equal(expectedStarted, result.Started);
        Assert.Equal(player, result.PlayerName);
    }

    [Fact]
    public void FindVlcPath_ShouldFindVlcInPath()
    {
        Directory.CreateDirectory(_directory);
        var executable = Path.Combine(_directory, "vlc.exe");
        File.WriteAllBytes(executable, []);
        Environment.SetEnvironmentVariable("PATH", _directory);

        Assert.True(File.Exists(WindowsPlaybackLauncher.FindVlcPath()));
    }

    [Fact]
    public async Task LaunchAsync_ShouldRespectCancellationBeforeOpeningAnything()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() => new WindowsPlaybackLauncher().LaunchAsync("C:\\video.mp4", new PlaybackLaunchOptions(1), cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void BuildVlcArguments_ShouldRequestFullscreenOnTheSelectedScreen()
    {
        var arguments = WindowsPlaybackLauncher.BuildVlcArguments("C:\\video.mp4", new PlaybackLaunchOptions(2));

        Assert.Contains("--fullscreen", arguments);
        Assert.Contains("--qt-fullscreen-screennumber=2", arguments);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
