using Sinalo.Application.Timer;

namespace Sinalo.Tests.Unit;

public sealed class TimerSessionTests
{
    [Fact]
    public void CountUp_StartPauseAndReset_TracksElapsedTime()
    {
        var now = new MutableClock(DateTimeOffset.UnixEpoch);
        var timer = new TimerSession(now.Read);
        timer.Configure(TimerDirection.CountUp, TimeSpan.FromMinutes(1));
        timer.Start(); now.Advance(TimeSpan.FromSeconds(12));
        Assert.Equal(TimeSpan.FromSeconds(12), timer.GetSnapshot().DisplayTime);
        timer.Pause(); now.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(TimeSpan.FromSeconds(12), timer.GetSnapshot().DisplayTime);
        timer.Reset();
        Assert.Equal(TimerRunState.Stopped, timer.GetSnapshot().State);
    }

    [Fact]
    public void CountDown_CompletesAtZeroAndRestartsFromTheConfiguredDuration()
    {
        var now = new MutableClock(DateTimeOffset.UnixEpoch);
        var timer = new TimerSession(now.Read);
        timer.Configure(TimerDirection.CountDown, TimeSpan.FromSeconds(5));
        timer.Start(); now.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal(TimerRunState.Completed, timer.GetSnapshot().State);
        Assert.Equal(TimeSpan.Zero, timer.GetSnapshot().DisplayTime);
        timer.Start();
        Assert.Equal(TimeSpan.FromSeconds(5), timer.GetSnapshot().DisplayTime);
    }

    [Fact]
    public void Configure_RejectsNegativeCountdownDuration() => Assert.Throws<ArgumentOutOfRangeException>(() => new TimerSession().Configure(TimerDirection.CountDown, TimeSpan.FromSeconds(-1)));

    private sealed class MutableClock(DateTimeOffset value)
    {
        private DateTimeOffset _value = value;
        public DateTimeOffset Read() => _value;
        public void Advance(TimeSpan duration) => _value += duration;
    }
}
