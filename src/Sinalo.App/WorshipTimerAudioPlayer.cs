using System.IO;
using System.Windows.Media;
using Sinalo.Application.WorshipTimer;

namespace Sinalo.App;

public enum WorshipTimerAudioPlaybackState { Stopped, Playing, Paused }
public enum WorshipTimerAudioPlaybackOrigin { Manual, Automatic }

public sealed record WorshipTimerAudioSnapshot(
    WorshipTimerAudioCue? Cue,
    WorshipTimerAudioPlaybackState State,
    WorshipTimerAudioPlaybackOrigin? Origin,
    TimeSpan Position,
    TimeSpan Duration,
    double Volume)
{
    public static WorshipTimerAudioSnapshot Initial { get; } = new(null, WorshipTimerAudioPlaybackState.Stopped, null, TimeSpan.Zero, TimeSpan.Zero, 0.8);
}

public interface IWorshipTimerAudioPlayer
{
    event EventHandler<WorshipTimerAudioSnapshot>? StateChanged;
    WorshipTimerAudioSnapshot Snapshot { get; }
    void Play(WorshipTimerAudioCue cue, WorshipTimerAudioPlaybackOrigin origin);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(double volume);
    void Refresh();
}

public sealed class WorshipTimerAudioPlayer : IWorshipTimerAudioPlayer
{
    private readonly MediaPlayer _player = new();
    private WorshipTimerAudioSnapshot _snapshot = WorshipTimerAudioSnapshot.Initial;

    public WorshipTimerAudioPlayer()
    {
        _player.MediaOpened += (_, _) =>
        {
            var duration = _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
            SetSnapshot(_snapshot with { Duration = duration, State = WorshipTimerAudioPlaybackState.Playing });
        };
        _player.MediaEnded += (_, _) => Stop();
        _player.MediaFailed += (_, _) => Stop();
    }

    public event EventHandler<WorshipTimerAudioSnapshot>? StateChanged;
    public WorshipTimerAudioSnapshot Snapshot => _snapshot;

    public void Play(WorshipTimerAudioCue cue, WorshipTimerAudioPlaybackOrigin origin)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "audio", "cronometro-culto", GetFileName(cue));
        if (!File.Exists(path))
        {
            Stop();
            return;
        }

        _player.Stop();
        _player.Open(new Uri(path));
        _player.Volume = _snapshot.Volume;
        SetSnapshot(new(cue, WorshipTimerAudioPlaybackState.Playing, origin, TimeSpan.Zero, TimeSpan.Zero, _snapshot.Volume));
        _player.Play();
    }

    public void Pause()
    {
        if (_snapshot.State != WorshipTimerAudioPlaybackState.Playing) return;
        _player.Pause();
        Refresh(WorshipTimerAudioPlaybackState.Paused);
    }

    public void Resume()
    {
        if (_snapshot.State != WorshipTimerAudioPlaybackState.Paused) return;
        _player.Play();
        Refresh(WorshipTimerAudioPlaybackState.Playing);
    }

    public void Stop()
    {
        _player.Stop();
        SetSnapshot(new(null, WorshipTimerAudioPlaybackState.Stopped, null, TimeSpan.Zero, TimeSpan.Zero, _snapshot.Volume));
    }

    public void Seek(TimeSpan position)
    {
        if (_snapshot.Cue is null) return;

        var maximum = _snapshot.Duration > TimeSpan.Zero ? _snapshot.Duration : position;
        var boundedPosition = TimeSpan.FromTicks(Math.Clamp(position.Ticks, TimeSpan.Zero.Ticks, maximum.Ticks));
        _player.Position = boundedPosition;
        SetSnapshot(_snapshot with { Position = boundedPosition });
    }

    public void SetVolume(double volume)
    {
        volume = Math.Clamp(volume, 0, 1);
        _player.Volume = volume;
        SetSnapshot(_snapshot with { Volume = volume });
    }

    public void Refresh() => Refresh(_snapshot.State);

    private void Refresh(WorshipTimerAudioPlaybackState state)
    {
        if (_snapshot.Cue is null) return;
        SetSnapshot(_snapshot with { State = state, Position = _player.Position });
    }

    private void SetSnapshot(WorshipTimerAudioSnapshot snapshot)
    {
        _snapshot = snapshot;
        StateChanged?.Invoke(this, snapshot);
    }

    private static string GetFileName(WorshipTimerAudioCue cue) => cue switch
    {
        WorshipTimerAudioCue.Opening => "abertura_escsb.mp3",
        WorshipTimerAudioCue.FiveMinutes => "5minutos_escsb.mp3",
        WorshipTimerAudioCue.OneMinute => "1minuto_escsb.mp3",
        _ => throw new ArgumentOutOfRangeException(nameof(cue))
    };
}
