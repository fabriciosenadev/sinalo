using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Timer;

namespace Sinalo.App.ViewModels;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed partial class TimerViewModel : ObservableObject
{
    private readonly TimerSession _session;

    public TimerViewModel(TimerSession session, TimerConfiguration configuration)
    {
        _session = session;
        _session.Configure(configuration.Direction, configuration.CountdownDuration);
        selectedDirection = Directions.First(option => option.Value == configuration.Direction);
        selectedFormat = DisplayFormats.FirstOrDefault(option => option.Value == configuration.DisplayFormat) ?? DisplayFormats[1];
        countdownDurationText = FormatDuration(configuration.CountdownDuration);
        Refresh();
    }

    public IReadOnlyList<TimerDirectionOption> Directions { get; } =
    [new(TimerDirection.CountUp, "Crescente"), new(TimerDirection.CountDown, "Decrescente")];

    public IReadOnlyList<TimerFormatOption> DisplayFormats { get; } =
    [new("hh:mm", "HH:mm"), new("hh:mm:ss", "HH:mm:ss"), new("hh:mm:ss.zzz", "HH:mm:ss.milisegundos"), new("nn:ss", "MM:ss"), new("nn:ss.zzz", "MM:ss.milisegundos")];

    [ObservableProperty] private TimerDirectionOption selectedDirection = new(TimerDirection.CountUp, "Crescente");
    [ObservableProperty] private TimerFormatOption selectedFormat = new("hh:mm:ss", "HH:mm:ss");
    [ObservableProperty] private string countdownDurationText = "00:01:00";
    [ObservableProperty] private string displayTime = "00:00:00";
    [ObservableProperty] private string stateLabel = "Parado";
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private bool isCountdown;

    public TimerConfiguration Configuration => new(SelectedDirection.Value, ParseDuration(CountdownDurationText), SelectedFormat.Value);
    public string StartPauseLabel => IsRunning ? "Pausar" : "Iniciar";

    public void ApplyConfiguration()
    {
        var configuration = Configuration;
        _session.Configure(configuration.Direction, configuration.CountdownDuration);
        Refresh();
    }

    public void StartOrPause()
    {
        if (_session.State == TimerRunState.Running) _session.Pause();
        else _session.Start();
        Refresh();
    }

    public void Reset()
    {
        _session.Reset();
        Refresh();
    }

    public TimerSnapshot Refresh()
    {
        var snapshot = _session.GetSnapshot();
        DisplayTime = Format(snapshot.DisplayTime);
        StateLabel = snapshot.State switch
        {
            TimerRunState.Running => "Em execução",
            TimerRunState.Paused => "Pausado",
            TimerRunState.Completed => "Tempo encerrado",
            _ => "Parado"
        };
        IsRunning = snapshot.State == TimerRunState.Running;
        IsCountdown = snapshot.Direction == TimerDirection.CountDown;
        OnPropertyChanged(nameof(StartPauseLabel));
        return snapshot;
    }

    public PresentationTimerData GetPresentationData()
    {
        var snapshot = Refresh();
        var progress = snapshot.Direction == TimerDirection.CountDown
            ? Math.Clamp(snapshot.DisplayTime.TotalMilliseconds, 0, snapshot.CountdownDuration.TotalMilliseconds)
            : snapshot.Elapsed.TotalMilliseconds;
        var maximum = snapshot.Direction == TimerDirection.CountDown
            ? Math.Max(1, snapshot.CountdownDuration.TotalMilliseconds)
            : Math.Max(1, snapshot.Elapsed.TotalMilliseconds + 1000);
        return new(DisplayTime, $"{SelectedDirection.Label} · {StateLabel}", progress, maximum);
    }

    private string Format(TimeSpan value) => SelectedFormat.Value switch
    {
        "hh:mm" => value.ToString(@"hh\:mm"),
        "hh:mm:ss" => value.ToString(@"hh\:mm\:ss"),
        "hh:mm:ss.zzz" => value.ToString(@"hh\:mm\:ss\.fff"),
        "nn:ss" => value.ToString(@"mm\:ss"),
        "nn:ss.zzz" => value.ToString(@"mm\:ss\.fff"),
        _ => value.ToString(@"hh\:mm\:ss")
    };

    private static TimeSpan ParseDuration(string value) => TimeSpan.TryParse(value, out var duration) && duration >= TimeSpan.Zero
        ? duration
        : throw new FormatException("Informe uma duração válida no formato HH:MM:SS.");

    private static string FormatDuration(TimeSpan duration) => duration.ToString(@"hh\:mm\:ss");
}

public sealed record TimerDirectionOption(TimerDirection Value, string Label);
public sealed record TimerFormatOption(string Value, string Label);
public sealed record PresentationTimerData(string DisplayTime, string Status, double ProgressValue, double ProgressMaximum);
