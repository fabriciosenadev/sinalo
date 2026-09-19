namespace Sinalo.Application.WorshipTimer;

public enum WorshipTimerMode { TargetTime, Duration }
public enum WorshipTimerAudioCue { Opening, FiveMinutes, OneMinute }

public sealed record WorshipTimerConfiguration(
    WorshipTimerMode Mode,
    TimeOnly TargetTime,
    TimeSpan Duration,
    bool StopAtZero,
    bool PlayOpening,
    bool PlayFiveMinutes,
    bool PlayOneMinute,
    WorshipTimerAudioCue SelectedAudioCue = WorshipTimerAudioCue.Opening,
    double AudioVolume = 0.8)
{
    public static WorshipTimerConfiguration Default { get; } = new(WorshipTimerMode.TargetTime, new TimeOnly(10, 0), TimeSpan.FromMinutes(40), true, true, true, true);
}

public interface IWorshipTimerConfigurationService
{
    Task<WorshipTimerConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorshipTimerConfiguration configuration, CancellationToken cancellationToken = default);
}

public sealed record WorshipTimerSnapshot(
    bool IsRunning,
    DateTime TargetAt,
    TimeSpan Remaining,
    bool IsExpired,
    double ProgressValue,
    double ProgressMaximum,
    IReadOnlyList<WorshipTimerAudioCue> DueCues);

public sealed class WorshipTimerSession(Func<DateTime>? now = null)
{
    private readonly Func<DateTime> _now = now ?? (() => DateTime.Now);
    private WorshipTimerConfiguration _configuration = WorshipTimerConfiguration.Default;
    private DateTime? _targetAt;
    private bool _openingPlayed;
    private bool _fiveMinutesPlayed;
    private bool _oneMinutePlayed;

    public WorshipTimerConfiguration Configuration => _configuration;
    public bool IsRunning => _targetAt is not null;

    public void Configure(WorshipTimerConfiguration configuration)
    {
        if (configuration.Duration <= TimeSpan.Zero || configuration.Duration > TimeSpan.FromHours(4))
            throw new ArgumentOutOfRangeException(nameof(configuration), "A duração deve estar entre 1 minuto e 4 horas.");

        _configuration = configuration;
        Stop();
    }

    public WorshipTimerSnapshot Start()
    {
        var current = _now();
        _targetAt = _configuration.Mode == WorshipTimerMode.TargetTime
            ? current.Date.Add(_configuration.TargetTime.ToTimeSpan())
            : current.Add(_configuration.Duration);
        if (_targetAt <= current) _targetAt = _targetAt.Value.AddDays(1);

        _openingPlayed = _fiveMinutesPlayed = _oneMinutePlayed = false;
        return GetSnapshot(includeOpeningCue: true);
    }

    public void Stop() => _targetAt = null;

    public WorshipTimerSnapshot AdjustMinutes(int minutes)
    {
        if (_targetAt is not null) _targetAt = _targetAt.Value.AddMinutes(minutes);
        return GetSnapshot();
    }

    public WorshipTimerSnapshot GetSnapshot() => GetSnapshot(includeOpeningCue: false);

    private WorshipTimerSnapshot GetSnapshot(bool includeOpeningCue)
    {
        var current = _now();
        if (_targetAt is null)
            return new(false, current, TimeSpan.Zero, false, 0, Math.Max(1, _configuration.Duration.TotalSeconds), []);

        var remaining = _targetAt.Value - current;
        var cues = new List<WorshipTimerAudioCue>();
        if (includeOpeningCue && _configuration.PlayOpening && !_openingPlayed)
        {
            _openingPlayed = true;
            cues.Add(WorshipTimerAudioCue.Opening);
        }
        if (remaining <= TimeSpan.FromMinutes(5) && remaining > TimeSpan.Zero && _configuration.PlayFiveMinutes && !_fiveMinutesPlayed)
        {
            _fiveMinutesPlayed = true;
            cues.Add(WorshipTimerAudioCue.FiveMinutes);
        }
        if (remaining <= TimeSpan.FromMinutes(1) && remaining > TimeSpan.Zero && _configuration.PlayOneMinute && !_oneMinutePlayed)
        {
            _oneMinutePlayed = true;
            cues.Add(WorshipTimerAudioCue.OneMinute);
        }

        var expired = remaining <= TimeSpan.Zero;
        if (expired && _configuration.StopAtZero)
        {
            _targetAt = null;
            remaining = TimeSpan.Zero;
        }

        var maximum = Math.Max(1, _configuration.Duration.TotalSeconds);
        var value = Math.Clamp(remaining.TotalSeconds, 0, maximum);
        return new(_targetAt is not null, _targetAt ?? current, remaining, expired, value, maximum, cues);
    }
}
