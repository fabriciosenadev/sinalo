using System.Diagnostics;
using System.IO;
using Sinalo.Application.Playback;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class MpvControllerTests
{
    [Fact]
    public async Task ControlsConfirmedPlaybackAndReusesProcessWithoutActivityGap()
    {
        using var video = await PlaybackVideoFixture.CreateAsync();
        await using var player = new MpvPlaybackLauncher();
        var reasons = new List<PlaybackEndReason?>();
        var activity = new List<bool>();
        player.Ended += snapshot => { lock (reasons) reasons.Add(snapshot.EndReason); };
        player.PlaybackActivityChanged += active => { lock (activity) activity.Add(active); };
        Assert.True((await player.LaunchAsync(video.Path, new(1) { ContentId = "a", Title = "Primeiro" })).Started);
        var process = player.ProcessId;
        Assert.Equal("Primeiro", player.Current.Title);
        Assert.True(player.Current.DurationSeconds > 0);
        Assert.True((await player.SetPausedAsync(true)).Succeeded);
        Assert.Equal(PlaybackState.Paused, player.Current.State);
        Assert.True((await player.SeekAsync(2)).Succeeded);
        Assert.Equal(PlaybackState.Paused, player.Current.State);
        Assert.InRange(player.Current.PositionSeconds, 1.5, 2.5);
        Assert.True((await player.SetVolumeAsync(35)).Succeeded);
        Assert.Equal(35, player.Current.Volume);
        Assert.True((await player.SetMutedAsync(true)).Succeeded);
        Assert.True(player.Current.IsMuted);
        Assert.True((await player.RestartAsync()).Succeeded);
        Assert.Equal(PlaybackState.Playing, player.Current.State);
        Assert.True((await player.LaunchAsync(video.Path, new(1) { ContentId = "b", Title = "Segundo" })).Started);
        Assert.Equal(process, player.ProcessId);
        Assert.Equal("b", player.Current.ContentId);
        Assert.Equal(35, player.Current.Volume);
        Assert.True(player.Current.IsMuted);
        Assert.True((await player.StopMediaAsync()).Succeeded);
        Assert.Equal(PlaybackState.Ended, player.Current.State);
        Assert.Equal(PlaybackEndReason.Stopped, player.Current.EndReason);
        Assert.Equal(process, player.ProcessId);
        Assert.Equal(new PlaybackEndReason?[] { PlaybackEndReason.Replaced, PlaybackEndReason.Stopped }, reasons);
        Assert.Equal(new[] { true, false }, activity);
        Assert.False((await player.SetPausedAsync(false)).Succeeded);
    }

    [Fact]
    public async Task NaturalEndIsDifferentFromExternalCloseAndPlayerRecovers()
    {
        using var video = await PlaybackVideoFixture.CreateAsync(3);
        await using var player = new MpvPlaybackLauncher();
        var natural = new TaskCompletionSource<PlaybackSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.Ended += snapshot => natural.TrySetResult(snapshot);
        Assert.True((await player.LaunchAsync(video.Path, new(1))).Started);
        Assert.Equal(PlaybackEndReason.Natural, (await natural.Task.WaitAsync(TimeSpan.FromSeconds(8))).EndReason);
        var closed = new TaskCompletionSource<PlaybackSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.Ended += snapshot => closed.TrySetResult(snapshot);
        Assert.True((await player.LaunchAsync(video.Path, new(1))).Started);
        using (var process = Process.GetProcessById(player.ProcessId!.Value)) { process.Kill(); await process.WaitForExitAsync(); }
        Assert.Equal(PlaybackEndReason.ExternalClosed, (await closed.Task.WaitAsync(TimeSpan.FromSeconds(5))).EndReason);
        Assert.False(player.Current.IsActive);
        Assert.True((await player.LaunchAsync(video.Path, new(1))).Started);
        await player.DisposeAsync();
        Assert.Null(player.ProcessId);
        Assert.Equal(PlaybackEndReason.ApplicationShutdown, player.Current.EndReason);
    }

    [Fact]
    public async Task InvalidFileIsRejectedWithoutFallbackOrFalseSuccess()
    {
        var file = Path.Combine(Path.GetTempPath(), $"sinalo-bad-{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        try
        {
            await using var player = new MpvPlaybackLauncher();
            var result = await player.LaunchAsync(file, new(1));
            Assert.False(result.Started);
            Assert.False(result.AllowsFallback);
            Assert.Equal(PlaybackState.Failed, player.Current.State);
            Assert.Null(player.ProcessId);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task RejectsInvalidControlValuesAndClampsVolume()
    {
        using var video = await PlaybackVideoFixture.CreateAsync();
        await using var player = new MpvPlaybackLauncher();
        Assert.True((await player.LaunchAsync(video.Path, new(1))).Started);
        Assert.False((await player.SeekAsync(double.NaN)).Succeeded);
        Assert.False((await player.SetVolumeAsync(double.PositiveInfinity)).Succeeded);
        Assert.True((await player.SetVolumeAsync(200)).Succeeded);
        Assert.Equal(100, player.Current.Volume);
        Assert.True((await player.SetVolumeAsync(-1)).Succeeded);
        Assert.Equal(0, player.Current.Volume);
        Assert.True((await player.SetPausedAsync(false)).Succeeded);
        Assert.True((await player.SetMutedAsync(false)).Succeeded);
        await player.StopMediaAsync();
    }
}
