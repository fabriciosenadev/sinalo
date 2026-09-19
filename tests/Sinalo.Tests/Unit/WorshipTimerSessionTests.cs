using Sinalo.Application.WorshipTimer;

namespace Sinalo.Tests.Unit;

public sealed class WorshipTimerSessionTests
{
    [Fact]
    public void Start_WithTargetTimeInPast_ShouldScheduleNextDayAndPlayOpeningOnce()
    {
        var now = new DateTime(2026, 9, 18, 11, 0, 0);
        var session = new WorshipTimerSession(() => now);
        session.Configure(new(WorshipTimerMode.TargetTime, new TimeOnly(10, 0), TimeSpan.FromMinutes(40), true, true, true, true));

        var snapshot = session.Start();

        Assert.Equal(new DateTime(2026, 9, 19, 10, 0, 0), snapshot.TargetAt);
        Assert.Contains(WorshipTimerAudioCue.Opening, snapshot.DueCues);
        Assert.Empty(session.GetSnapshot().DueCues);
    }

    [Fact]
    public void GetSnapshot_ShouldTriggerWarningsOnlyOnceAndStopAtZero()
    {
        var now = new DateTime(2026, 9, 18, 9, 50, 0);
        var session = new WorshipTimerSession(() => now);
        session.Configure(new(WorshipTimerMode.Duration, new TimeOnly(10, 0), TimeSpan.FromMinutes(10), true, false, true, true));
        session.Start();

        now = now.AddMinutes(5);
        Assert.Contains(WorshipTimerAudioCue.FiveMinutes, session.GetSnapshot().DueCues);
        Assert.Empty(session.GetSnapshot().DueCues);

        now = now.AddMinutes(4);
        Assert.Contains(WorshipTimerAudioCue.OneMinute, session.GetSnapshot().DueCues);

        now = now.AddMinutes(1);
        var ended = session.GetSnapshot();
        Assert.False(ended.IsRunning);
        Assert.True(ended.IsExpired);
        Assert.Equal(TimeSpan.Zero, ended.Remaining);
    }

    [Fact]
    public void Configure_WithInvalidDuration_ShouldRejectTheConfiguration()
    {
        var session = new WorshipTimerSession();

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Configure(
            new(WorshipTimerMode.Duration, new TimeOnly(10, 0), TimeSpan.Zero, true, true, true, true)));
    }

    [Fact]
    public void GetSnapshot_WhenStopAtZeroIsDisabled_ShouldKeepNegativeTimeRunning()
    {
        var now = new DateTime(2026, 9, 18, 10, 0, 0);
        var session = new WorshipTimerSession(() => now);
        session.Configure(new(WorshipTimerMode.Duration, new TimeOnly(10, 0), TimeSpan.FromMinutes(1), false, false, false, false));
        session.Start();

        now = now.AddMinutes(2);
        var snapshot = session.GetSnapshot();

        Assert.True(snapshot.IsRunning);
        Assert.True(snapshot.IsExpired);
        Assert.Equal(TimeSpan.FromMinutes(-1), snapshot.Remaining);
    }

    [Fact]
    public void AdjustMinutes_WhenStopped_ShouldKeepTheTimerStopped()
    {
        var session = new WorshipTimerSession();

        var snapshot = session.AdjustMinutes(5);

        Assert.False(snapshot.IsRunning);
        Assert.Equal(TimeSpan.Zero, snapshot.Remaining);
    }
}
