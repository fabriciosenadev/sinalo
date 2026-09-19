using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.WorshipTimer;

namespace Sinalo.App.ViewModels;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed partial class WorshipTimerViewModel : ObservableObject
{
    private readonly WorshipTimerSession _session;
    private readonly IWorshipTimerAudioPlayer _audioPlayer;

    public WorshipTimerViewModel(WorshipTimerSession session, IWorshipTimerAudioPlayer audioPlayer, WorshipTimerConfiguration? configuration = null)
    {
        _session = session;
        _audioPlayer = audioPlayer;
        var initial = configuration ?? WorshipTimerConfiguration.Default;
        _session.Configure(initial);
        selectedMode = Modes.First(option => option.Value == initial.Mode);
        targetTimeText = initial.TargetTime.ToString("HH:mm");
        durationMinutesText = ((int)initial.Duration.TotalMinutes).ToString();
        stopAtZero = initial.StopAtZero;
        playOpening = initial.PlayOpening;
        playFiveMinutes = initial.PlayFiveMinutes;
        playOneMinute = initial.PlayOneMinute;
        selectedAudioCue = AudioCues.First(option => option.Value == initial.SelectedAudioCue);
        audioVolume = Math.Clamp(initial.AudioVolume, 0, 1);
        _audioPlayer.StateChanged += (_, snapshot) => ApplyAudioSnapshot(snapshot);
        _audioPlayer.SetVolume(audioVolume);
        Refresh();
    }

    public IReadOnlyList<WorshipTimerModeOption> Modes { get; } = [new(WorshipTimerMode.TargetTime, "Hora de término"), new(WorshipTimerMode.Duration, "Duração")];
    public IReadOnlyList<WorshipTimerAudioCueOption> AudioCues { get; } =
    [new(WorshipTimerAudioCue.Opening, "Abertura"), new(WorshipTimerAudioCue.FiveMinutes, "Aviso de 5 minutos"), new(WorshipTimerAudioCue.OneMinute, "Aviso de 1 minuto")];
    [ObservableProperty] private WorshipTimerModeOption selectedMode = new(WorshipTimerMode.TargetTime, "Hora de término");
    [ObservableProperty] private WorshipTimerAudioCueOption selectedAudioCue = new(WorshipTimerAudioCue.Opening, "Abertura");
    [ObservableProperty] private string targetTimeText = "10:00";
    [ObservableProperty] private string durationMinutesText = "40";
    [ObservableProperty] private bool stopAtZero = true;
    [ObservableProperty] private bool playOpening = true;
    [ObservableProperty] private bool playFiveMinutes = true;
    [ObservableProperty] private bool playOneMinute = true;
    [ObservableProperty] private string remainingTime = "00:00:00";
    [ObservableProperty] private string currentTime = "00:00";
    [ObservableProperty] private string targetTimeLabel = "Defina o horário e inicie.";
    [ObservableProperty] private string stateLabel = "Parado";
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private bool isExpired;
    [ObservableProperty] private double progressValue;
    [ObservableProperty] private double progressMaximum = 1;
    [ObservableProperty] private double audioPositionSeconds;
    [ObservableProperty] private double audioDurationSeconds = 1;
    [ObservableProperty] private double audioVolume = 0.8;
    [ObservableProperty] private string audioStateLabel = "Parado";
    [ObservableProperty] private string audioOriginLabel = "Selecione um áudio para testar.";
    [ObservableProperty] private bool isAudioPlaying;
    [ObservableProperty] private bool isAudioPaused;

    public bool IsTargetTimeMode => SelectedMode.Value == WorshipTimerMode.TargetTime;
    public bool IsDurationMode => SelectedMode.Value == WorshipTimerMode.Duration;
    public bool IsConfigurationEditable => !IsRunning;
    public bool CanEditTargetTime => IsConfigurationEditable && IsTargetTimeMode;
    public bool CanEditDuration => IsConfigurationEditable && IsDurationMode;
    public bool CanControlAudio => IsAudioPlaying || IsAudioPaused;
    public string StartStopLabel => IsRunning ? "Desligar" : "Ligar";
    public string PauseResumeAudioLabel => IsAudioPaused ? "Continuar" : "Pausar";
    public string AudioPositionLabel => FormatAudioTime(AudioPositionSeconds);
    public string AudioDurationLabel => FormatAudioTime(AudioDurationSeconds);

    partial void OnSelectedModeChanged(WorshipTimerModeOption value)
    {
        OnPropertyChanged(nameof(IsTargetTimeMode));
        OnPropertyChanged(nameof(IsDurationMode));
        OnPropertyChanged(nameof(CanEditTargetTime));
        OnPropertyChanged(nameof(CanEditDuration));
    }
    partial void OnAudioVolumeChanged(double value) => _audioPlayer.SetVolume(value);

    public WorshipTimerConfiguration Configuration => BuildConfiguration();

    public void ApplyConfiguration()
    {
        _session.Configure(BuildConfiguration());
        Refresh();
    }

    public IReadOnlyList<WorshipTimerAudioCue> StartOrStop()
    {
        if (IsRunning)
        {
            _session.Stop();
            _audioPlayer.Stop();
            Refresh();
            return [];
        }

        _session.Configure(BuildConfiguration());
        var snapshot = _session.Start();
        Apply(snapshot);
        return snapshot.DueCues;
    }

    public IReadOnlyList<WorshipTimerAudioCue> AdjustMinutes(int minutes)
    {
        var snapshot = _session.AdjustMinutes(minutes);
        Apply(snapshot);
        return snapshot.DueCues;
    }

    public IReadOnlyList<WorshipTimerAudioCue> Refresh()
    {
        var snapshot = _session.GetSnapshot();
        Apply(snapshot);
        _audioPlayer.Refresh();
        return snapshot.DueCues;
    }

    public void PlaySelectedAudio() => _audioPlayer.Play(SelectedAudioCue.Value, WorshipTimerAudioPlaybackOrigin.Manual);
    public void PlayAutomaticCue(WorshipTimerAudioCue cue) => _audioPlayer.Play(cue, WorshipTimerAudioPlaybackOrigin.Automatic);
    public void PauseOrResumeAudio()
    {
        if (IsAudioPaused) _audioPlayer.Resume();
        else _audioPlayer.Pause();
    }
    public void StopAudio() => _audioPlayer.Stop();
    public void SeekAudio(double seconds) => _audioPlayer.Seek(TimeSpan.FromSeconds(Math.Max(0, seconds)));

    public PresentationTimerData GetPresentationData() => new(RemainingTime, $"Hora atual {CurrentTime} · {TargetTimeLabel}", ProgressValue, ProgressMaximum);

    private WorshipTimerConfiguration BuildConfiguration()
    {
        if (!TimeOnly.TryParse(TargetTimeText, out var target)) throw new FormatException("Informe a hora de término no formato HH:MM.");
        if (!int.TryParse(DurationMinutesText, out var minutes) || minutes is < 1 or > 240) throw new FormatException("Informe uma duração entre 1 e 240 minutos.");
        return new(SelectedMode.Value, target, TimeSpan.FromMinutes(minutes), StopAtZero, PlayOpening, PlayFiveMinutes, PlayOneMinute, SelectedAudioCue.Value, AudioVolume);
    }

    private void Apply(WorshipTimerSnapshot snapshot)
    {
        CurrentTime = DateTime.Now.ToString("HH:mm");
        RemainingTime = Format(snapshot.Remaining);
        TargetTimeLabel = snapshot.IsRunning ? $"Término previsto: {snapshot.TargetAt:HH:mm}" : "Defina o horário e inicie.";
        StateLabel = snapshot.IsExpired ? "Tempo encerrado" : snapshot.IsRunning ? "Em execução" : "Parado";
        IsRunning = snapshot.IsRunning;
        IsExpired = snapshot.IsExpired;
        ProgressValue = snapshot.ProgressValue;
        ProgressMaximum = snapshot.ProgressMaximum;
        OnPropertyChanged(nameof(StartStopLabel));
        OnPropertyChanged(nameof(IsConfigurationEditable));
        OnPropertyChanged(nameof(CanEditTargetTime));
        OnPropertyChanged(nameof(CanEditDuration));
    }

    private void ApplyAudioSnapshot(WorshipTimerAudioSnapshot snapshot)
    {
        AudioPositionSeconds = Math.Clamp(snapshot.Position.TotalSeconds, 0, Math.Max(1, snapshot.Duration.TotalSeconds));
        AudioDurationSeconds = Math.Max(1, snapshot.Duration.TotalSeconds);
        AudioVolume = snapshot.Volume;
        IsAudioPlaying = snapshot.State == WorshipTimerAudioPlaybackState.Playing;
        IsAudioPaused = snapshot.State == WorshipTimerAudioPlaybackState.Paused;
        AudioStateLabel = snapshot.State switch
        {
            WorshipTimerAudioPlaybackState.Playing => "Tocando",
            WorshipTimerAudioPlaybackState.Paused => "Pausado",
            _ => "Parado"
        };
        AudioOriginLabel = snapshot.Cue is null
            ? "Selecione um áudio para testar."
            : $"{AudioCues.First(option => option.Value == snapshot.Cue).Label} · {(snapshot.Origin == WorshipTimerAudioPlaybackOrigin.Automatic ? "aviso automático" : "controle manual")}";
        OnPropertyChanged(nameof(PauseResumeAudioLabel));
        OnPropertyChanged(nameof(CanControlAudio));
        OnPropertyChanged(nameof(AudioPositionLabel));
        OnPropertyChanged(nameof(AudioDurationLabel));
    }

    private static string Format(TimeSpan value)
    {
        var prefix = value < TimeSpan.Zero ? "−" : string.Empty;
        return prefix + value.Duration().ToString(@"hh\:mm\:ss");
    }

    private static string FormatAudioTime(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss");
}

public sealed record WorshipTimerModeOption(WorshipTimerMode Value, string Label);
public sealed record WorshipTimerAudioCueOption(WorshipTimerAudioCue Value, string Label);
