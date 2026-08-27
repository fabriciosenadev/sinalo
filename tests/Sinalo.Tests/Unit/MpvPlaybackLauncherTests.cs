using System.IO;
using Sinalo.Application.Playback;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class MpvPlaybackLauncherTests
{
    [Fact]
    public void BuildStartArguments_ShouldUseAnIdlePlayerWithAnExclusiveSinaloPipe()
    {
        var arguments = MpvPlaybackLauncher.BuildStartArguments("sinalo-mpv-test");

        Assert.Contains("--idle=yes", arguments);
        Assert.Contains("--hwdec=auto-safe", arguments);
        Assert.Contains("--input-ipc-server=\\\\.\\pipe\\sinalo-mpv-test", arguments);
    }

    [Fact]
    public async Task LaunchAsync_ShouldReportFailureWhenTheBundledPlayerIsUnavailable()
    {
        await using var launcher = new MpvPlaybackLauncher(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mpv.exe"));

        var result = await launcher.LaunchAsync("C:\\video.mp4", new PlaybackLaunchOptions(1));

        Assert.False(result.Started);
        Assert.Contains("player rápido", result.Message);
    }

    [Fact]
    public async Task WarmAsync_ShouldKeepTheBundledPlayerReadyAndAcceptConsecutiveFileReplacements()
    {
        var videoPath = Path.Combine(Path.GetTempPath(), $"Sinalo-mpv-{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(videoPath, []);
        try
        {
            await using var launcher = new MpvPlaybackLauncher();

            await launcher.WarmAsync();
            await launcher.WarmAsync();
            var firstResult = await launcher.LaunchAsync(videoPath, new PlaybackLaunchOptions(1));
            var secondResult = await launcher.LaunchAsync(videoPath, new PlaybackLaunchOptions(1));

            Assert.True(firstResult.Started);
            Assert.Equal("MPV", firstResult.PlayerName);
            Assert.True(secondResult.Started);
            Assert.Equal("MPV", secondResult.PlayerName);
        }
        finally
        {
            if (File.Exists(videoPath)) File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task FallbackPlaybackLauncher_ShouldUseTheFallbackWhenThePrimaryFails()
    {
        var fallback = new RecordingLauncher(new PlaybackLaunchResult(true, "VLC", "Vídeo aberto no VLC."));
        var launcher = new FallbackPlaybackLauncher(new RecordingLauncher(new PlaybackLaunchResult(false, string.Empty, "MPV indisponível.")), fallback);

        var result = await launcher.LaunchAsync("C:\\video.mp4", new PlaybackLaunchOptions(1));

        Assert.True(result.Started);
        Assert.True(fallback.WasCalled);
    }

    [Fact]
    public async Task FallbackPlaybackLauncher_ShouldNotUseTheFallbackWhenThePrimaryStarts()
    {
        var fallback = new RecordingLauncher(new PlaybackLaunchResult(true, "VLC", "Vídeo aberto no VLC."));
        var launcher = new FallbackPlaybackLauncher(new RecordingLauncher(new PlaybackLaunchResult(true, "MPV", "Vídeo aberto no MPV.")), fallback);

        var result = await launcher.LaunchAsync("C:\\video.mp4", new PlaybackLaunchOptions(1));

        Assert.Equal("MPV", result.PlayerName);
        Assert.False(fallback.WasCalled);
    }

    private sealed class RecordingLauncher(PlaybackLaunchResult result) : IPlaybackLauncher
    {
        public bool WasCalled { get; private set; }
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
