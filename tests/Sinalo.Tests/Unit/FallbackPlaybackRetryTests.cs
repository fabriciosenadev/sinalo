using Sinalo.Application.Playback;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class FallbackPlaybackRetryTests
{
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task StopsAtFirstSuccessfulAttemptAndPreservesRequest(int successAttempt)
    {
        var calls = new List<string>();
        var primary = new Launcher(call => new(call == successAttempt, "MPV", "Tentativa"), calls, "MPV");
        var fallback = new Launcher(_ => new(true, "VLC", "Externo"), calls, "VLC");
        using var service = new FallbackPlaybackLauncher(primary, fallback);
        using var cancellation = new CancellationTokenSource();
        var options = new PlaybackLaunchOptions(2) { ContentId = "video", Title = "Vídeo selecionado" };

        var result = await service.LaunchAsync("video.mp4", options, cancellation.Token);

        Assert.True(result.Started); Assert.Equal("MPV", result.PlayerName);
        Assert.Equal(successAttempt, primary.Calls); Assert.Equal(0, fallback.Calls);
        Assert.All(primary.Requests, request =>
        {
            Assert.Equal("video.mp4", request.Path); Assert.Same(options, request.Options);
            Assert.Equal(cancellation.Token, request.Token);
        });
    }

    [Fact]
    public async Task FallsBackExactlyOnceAfterThreeFailuresAndResetsForNextRequest()
    {
        var calls = new List<string>();
        var primary = new Launcher(_ => new(false, "MPV", "Falha ao iniciar"), calls, "MPV");
        var fallback = new Launcher(_ => new(true, "VLC", "Externo"), calls, "VLC");
        using var service = new FallbackPlaybackLauncher(primary, fallback);
        var options = new PlaybackLaunchOptions(2);
        for (var request = 0; request < 2; request++)
        {
            var result = await service.LaunchAsync("video.mp4", options);
            Assert.Equal("VLC", result.PlayerName); Assert.True(result.Started);
        }
        Assert.Equal(new[] { "MPV", "MPV", "MPV", "VLC", "MPV", "MPV", "MPV", "VLC" }, calls);
        Assert.All(fallback.Requests, request => { Assert.Equal("video.mp4", request.Path); Assert.Same(options, request.Options); });
    }

    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task NonRetryableMediaFailureStopsWithoutFallback(int failureAttempt)
    {
        var calls = new List<string>();
        var primary = new Launcher(call => new(false, "MPV", "Falha", AllowsFallback: call != failureAttempt), calls, "MPV");
        var fallback = new Launcher(_ => new(true, "VLC", "Externo"), calls, "VLC");
        using var service = new FallbackPlaybackLauncher(primary, fallback);
        var result = await service.LaunchAsync("video.mp4", new(1));
        Assert.False(result.Started); Assert.False(result.AllowsFallback);
        Assert.Equal(failureAttempt, primary.Calls); Assert.Equal(0, fallback.Calls);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task CancellationPreventsRemainingAttemptsAndFallback(int cancelAfter)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = new List<string>();
        var primary = new Launcher(call =>
        {
            if (call == cancelAfter) cancellation.Cancel();
            return new(false, "MPV", "Falha ao iniciar");
        }, calls, "MPV");
        var fallback = new Launcher(_ => new(true, "VLC", "Externo"), calls, "VLC");
        using var service = new FallbackPlaybackLauncher(primary, fallback);
        if (cancelAfter == 0) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LaunchAsync("video.mp4", new(1), cancellation.Token));
        Assert.Equal(cancelAfter, primary.Calls); Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task DoesNotTreatUnhandledExceptionAsPermissionToRetryOrFallback()
    {
        var calls = new List<string>();
        var primary = new Launcher(_ => throw new InvalidOperationException("Falha inesperada"), calls, "MPV");
        var fallback = new Launcher(_ => new(true, "VLC", "Externo"), calls, "VLC");
        using var service = new FallbackPlaybackLauncher(primary, fallback);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync("video.mp4", new(1)));
        Assert.Equal(1, primary.Calls); Assert.Equal(0, fallback.Calls);
    }

    private sealed class Launcher(Func<int, PlaybackLaunchResult> result, List<string> order, string player) : IPlaybackLauncher
    {
        public int Calls { get; private set; }
        public List<(string Path, PlaybackLaunchOptions Options, CancellationToken Token)> Requests { get; } = [];
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
        {
            Calls++; order.Add(player); Requests.Add((filePath, options, cancellationToken));
            return Task.FromResult(result(Calls));
        }
    }
}
