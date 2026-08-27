using Sinalo.Application.Raffle;

namespace Sinalo.Tests.Unit;

public sealed class RaffleSessionTests
{
    [Fact]
    public void AddNameAndRange_ShouldKeepUniqueParticipantsAndFormatNumbers()
    {
        var raffle = new RaffleSession();

        raffle.AddName(" Ana ");
        raffle.AddName("ana");
        raffle.AddName(" ");
        raffle.AddRange(3, 1);

        var snapshot = raffle.Snapshot();
        Assert.Equal(4, snapshot.AvailableCount);
        Assert.Equal(["Ana", "0001", "0002", "0003"], snapshot.Participants.Select(item => item.Label));
    }

    [Fact]
    public void StartAndTick_ShouldCompleteDrawAndPreventWinnerRepetition()
    {
        var clock = new MutableClock(DateTimeOffset.UnixEpoch);
        var raffle = new RaffleSession(clock.Read, new FirstRandom());
        raffle.SetAnimationDuration(TimeSpan.FromSeconds(2));
        raffle.AddName("Ana");
        raffle.AddName("Bruno");

        raffle.Start();
        Assert.Equal(RaffleState.Animating, raffle.State);
        Assert.Throws<InvalidOperationException>(() => raffle.AddName("Carla"));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(RaffleState.Animating, raffle.Tick().State);

        clock.Advance(TimeSpan.FromSeconds(1));
        var completed = raffle.Tick();

        Assert.Equal(RaffleState.Completed, completed.State);
        Assert.Equal("Ana", completed.CurrentLabel);
        Assert.Equal(1, completed.AvailableCount);
        Assert.Equal(1, completed.DrawnCount);

        raffle.Start();
        clock.Advance(TimeSpan.FromSeconds(2));
        var second = raffle.Tick();
        Assert.Equal("Bruno", second.CurrentLabel);
        Assert.Equal(2, second.DrawnCount);
        Assert.Throws<InvalidOperationException>(() => raffle.Start());
    }

    [Fact]
    public void RestoreMarkDrawnRestartAndClear_ShouldManageParticipants()
    {
        var raffle = new RaffleSession();
        raffle.AddName("Ana");
        raffle.AddName("Bruno");
        var ana = raffle.Snapshot().Participants[0];

        raffle.MarkDrawn(ana.Id);
        Assert.Equal(1, raffle.Snapshot().DrawnCount);

        raffle.Restore(ana.Id);
        Assert.Equal(0, raffle.Snapshot().DrawnCount);

        raffle.MarkDrawn(ana.Id);
        raffle.Restart();
        Assert.Equal(2, raffle.Snapshot().AvailableCount);
        raffle.Clear();
        Assert.Empty(raffle.Snapshot().Participants);

        raffle.AddName("Ana");
        raffle.Start();
        raffle.Start();
        Assert.Equal(RaffleState.Animating, raffle.Tick().State);
        raffle.ResetDisplay();
        Assert.Equal(RaffleState.Animating, raffle.State);
        var participant = raffle.Snapshot().Participants[0];
        Assert.Throws<InvalidOperationException>(() => raffle.AddRange(1, 2));
        Assert.Throws<InvalidOperationException>(() => raffle.Restore(participant.Id));
        Assert.Throws<InvalidOperationException>(() => raffle.MarkDrawn(participant.Id));
        Assert.Throws<InvalidOperationException>(() => raffle.Restart());
        Assert.Throws<InvalidOperationException>(() => raffle.Clear());

        var now = raffle.Snapshot();
        Assert.Equal(RaffleState.Animating, now.State);
    }

    [Fact]
    public void SetAnimationDuration_ShouldRejectZeroOrNegativeValues()
    {
        var raffle = new RaffleSession();
        Assert.Throws<ArgumentOutOfRangeException>(() => raffle.SetAnimationDuration(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => raffle.SetAnimationDuration(TimeSpan.FromSeconds(-1)));
    }

    private sealed class MutableClock(DateTimeOffset value)
    {
        private DateTimeOffset _value = value;
        public DateTimeOffset Read() => _value;
        public void Advance(TimeSpan duration) => _value += duration;
    }

    private sealed class FirstRandom : Random
    {
        public override int Next(int maxValue) => 0;
    }
}
