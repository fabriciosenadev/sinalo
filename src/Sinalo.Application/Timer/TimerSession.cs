namespace Sinalo.Application.Timer;

public enum TimerDirection
{
    CountUp = 0,
    CountDown = 1
}

public enum TimerRunState
{
    Stopped,
    Running,
    Paused,
    Completed
}

public sealed record TimerConfiguration(TimerDirection Direction, TimeSpan CountdownDuration, string DisplayFormat);

public sealed record TimerSnapshot(
    TimerDirection Direction,
    TimerRunState State,
    TimeSpan Elapsed,
    TimeSpan DisplayTime,
    TimeSpan CountdownDuration);

public interface ITimerConfigurationService
{
    Task<TimerConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TimerConfiguration configuration, CancellationToken cancellationToken = default);
}

public sealed class TimerSession(Func<DateTimeOffset>? utcNow = null)
{
    private readonly Func<DateTimeOffset> _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    private DateTimeOffset? _startedAtUtc;
    private TimeSpan _elapsedBeforeStart;

    public TimerDirection Direction { get; private set; } = TimerDirection.CountUp;
    public TimeSpan CountdownDuration { get; private set; } = TimeSpan.FromMinutes(1);
    public TimerRunState State { get; private set; } = TimerRunState.Stopped;

    public void Configure(TimerDirection direction, TimeSpan countdownDuration)
    {
        if (countdownDuration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(countdownDuration));
        Direction = direction;
        CountdownDuration = countdownDuration;
        Reset();
    }

    public void Start()
    {
        var snapshot = GetSnapshot();
        if (snapshot.State == TimerRunState.Completed) Reset();
        if (State == TimerRunState.Running) return;
        _startedAtUtc = _utcNow();
        State = TimerRunState.Running;
    }

    public void Pause()
    {
        if (State != TimerRunState.Running) return;
        _elapsedBeforeStart = CurrentElapsed();
        _startedAtUtc = null;
        State = TimerRunState.Paused;
    }

    public void Reset()
    {
        _startedAtUtc = null;
        _elapsedBeforeStart = TimeSpan.Zero;
        State = TimerRunState.Stopped;
    }

    public TimerSnapshot GetSnapshot()
    {
        var elapsed = CurrentElapsed();
        var display = Direction == TimerDirection.CountDown
            ? CountdownDuration - elapsed
            : elapsed;

        if (Direction == TimerDirection.CountDown && display <= TimeSpan.Zero)
        {
            display = TimeSpan.Zero;
            _elapsedBeforeStart = CountdownDuration;
            _startedAtUtc = null;
            State = TimerRunState.Completed;
        }

        return new TimerSnapshot(Direction, State, elapsed, display, CountdownDuration);
    }

    private TimeSpan CurrentElapsed() => State == TimerRunState.Running && _startedAtUtc is { } startedAt
        ? _elapsedBeforeStart + (_utcNow() - startedAt)
        : _elapsedBeforeStart;
}
