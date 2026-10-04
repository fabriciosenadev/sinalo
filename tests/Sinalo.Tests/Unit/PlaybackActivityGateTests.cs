using Sinalo.Application.Playback;

namespace Sinalo.Tests.Unit;

public sealed class PlaybackActivityGateTests
{
    [Fact]
    public async Task Gate_BlocksDuringPlaybackAndReleasesOnEnd()
    {
        var gate = new PlaybackActivityGate();
        var changes = new List<bool>();
        gate.Changed += changes.Add;
        await gate.WaitUntilIdleAsync();
        gate.SetActive(true);
        gate.SetActive(true);
        var wait = gate.WaitUntilIdleAsync();
        Assert.False(wait.IsCompleted);
        Assert.True(gate.IsActive);
        gate.SetActive(false);
        await wait;
        Assert.False(gate.IsActive);
        Assert.Equal([true, false], changes);
    }

    [Fact]
    public async Task Gate_WaitCanBeCancelled()
    {
        var gate = new PlaybackActivityGate();
        gate.SetActive(true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.WaitUntilIdleAsync(cancellation.Token));
        gate.SetActive(false);
    }
}
