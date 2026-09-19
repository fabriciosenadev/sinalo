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
}
