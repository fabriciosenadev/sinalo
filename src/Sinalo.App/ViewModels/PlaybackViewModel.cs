using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinalo.Application.Playback;

namespace Sinalo.App.ViewModels;

public sealed partial class PlaybackViewModel : ObservableObject, IDisposable
{
    private readonly IPlaybackController _controller;
    private readonly PlaybackService _service;
    private readonly Action<Action> _dispatch;
    private readonly Func<Task<PlaybackLaunchOptions?>>? _resolveReplayOutput;
    private readonly Action<Sinalo.Domain.ContentItem>? _replayed;
    private bool _disposed;
    private PlaybackSnapshot _snapshot;
    [ObservableProperty] private string title = "Nenhum vídeo em reprodução";
    [ObservableProperty] private string status = "Parado";
    [ObservableProperty] private string playerAndOutput = "";
    [ObservableProperty] private string message = "";
    [ObservableProperty] private double positionSeconds;
    [ObservableProperty] private double durationSeconds;
    [ObservableProperty] private double volume = 100;
    [ObservableProperty] private bool isBusy;
    private bool _seeking;
    private bool _volumeEditing;
    public PlaybackSnapshot Snapshot => _snapshot;
    public bool HasSession => _snapshot.SessionId != Guid.Empty;
    public bool CanTransport => !IsBusy && _snapshot.State is PlaybackState.Playing or PlaybackState.Paused;
    public bool CanSeek => CanTransport && _snapshot.Supports(PlaybackCapabilities.Seek) && DurationSeconds > 0;
    public bool CanVolume => CanTransport && _snapshot.Supports(PlaybackCapabilities.Volume);
    public string PlayPauseLabel => _snapshot.State == PlaybackState.Playing ? "Pausar" : _snapshot.State == PlaybackState.Paused ? "Continuar" : "Reproduzir";
    public string MuteLabel => _snapshot.IsMuted ? "Ativar som" : "Mudo";
    public string TimeLabel => $"{FormatTime(PositionSeconds)} / {(_snapshot.DurationSeconds is > 0 ? FormatTime(DurationSeconds) : "--:--")}";

    public PlaybackViewModel(IPlaybackController controller, PlaybackService service, Action<Action>? dispatch = null,
        Func<Task<PlaybackLaunchOptions?>>? resolveReplayOutput = null, Action<Sinalo.Domain.ContentItem>? replayed = null)
    {
        _controller = controller;
        _service = service;
        _dispatch = dispatch ?? (action => action());
        _resolveReplayOutput = resolveReplayOutput;
        _replayed = replayed;
        _snapshot = controller.Current;
        controller.Changed += OnChanged;
        Apply(_snapshot);
    }

    private void OnChanged(PlaybackSnapshot snapshot) => _dispatch(() => { if (!_disposed) Apply(snapshot); });
    private void Apply(PlaybackSnapshot snapshot)
    {
        if (_snapshot.SessionId != snapshot.SessionId) { _seeking = false; _volumeEditing = false; }
        _snapshot = snapshot;
        Title = snapshot.Title;
        PlayerAndOutput = string.IsNullOrEmpty(snapshot.Player) ? "" : $"{snapshot.Player} · {snapshot.Output?.OutputLabel}";
        Status = snapshot.State switch { PlaybackState.Loading => "Carregando...", PlaybackState.Playing => "Reproduzindo", PlaybackState.Paused => "Pausado", PlaybackState.Failed => "Falha", PlaybackState.Ended => "Encerrado", _ => "Parado" };
        DurationSeconds = snapshot.DurationSeconds ?? 0;
        if (!_seeking) PositionSeconds = snapshot.PositionSeconds;
        if (!_volumeEditing) Volume = snapshot.Volume;
        Message = snapshot.Error ?? (snapshot.IsActive && snapshot.Capabilities == PlaybackCapabilities.None ? "Use os controles do player externo. Feche-o antes de abrir outro vídeo ou a apresentação." : "");
        NotifyControls();
    }

    partial void OnPositionSecondsChanged(double value) => OnPropertyChanged(nameof(TimeLabel));
    partial void OnIsBusyChanged(bool value) => NotifyControls();
    private void NotifyControls()
    {
        foreach (var property in new[] { nameof(HasSession), nameof(CanTransport), nameof(CanSeek), nameof(CanVolume), nameof(PlayPauseLabel), nameof(MuteLabel), nameof(TimeLabel) }) OnPropertyChanged(property);
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        ToggleMuteCommand.NotifyCanExecuteChanged();
    }

    private bool CanPlayPause() => !IsBusy && (_snapshot.IsActive
        ? CanTransport && _snapshot.Supports(PlaybackCapabilities.Pause)
        : _snapshot.ContentId is not null && _snapshot.Output is not null);
    private bool CanStop() => CanTransport && _snapshot.Supports(PlaybackCapabilities.Stop);
    private bool CanRestart() => CanTransport && _snapshot.Supports(PlaybackCapabilities.Restart);
    private bool CanMute() => CanVolume;

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private async Task PlayPauseAsync()
    {
        if (_snapshot.IsActive) await ExecuteAsync(() => _controller.SetPausedAsync(_snapshot.State != PlaybackState.Paused));
        else if (_snapshot.ContentId is { } id && _snapshot.Output is { } output)
        {
            await ExecuteAsync(async () =>
            {
                var selectedOutput = _resolveReplayOutput is null ? output : await _resolveReplayOutput();
                if (selectedOutput is null) return new(false, "A tela selecionada não está disponível. Verifique a conexão do monitor.");
                var result = await _service.PlayAsync(id, selectedOutput);
                if (result.Started && result.Item is not null) _replayed?.Invoke(result.Item);
                return new(result.Started, result.Started ? null : result.Message);
            });
        }
    }
    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => ExecuteAsync(() => _controller.StopMediaAsync());
    [RelayCommand(CanExecute = nameof(CanRestart))]
    private Task RestartAsync() => ExecuteAsync(() => _controller.RestartAsync());
    [RelayCommand(CanExecute = nameof(CanMute))]
    private Task ToggleMuteAsync() => ExecuteAsync(() => _controller.SetMutedAsync(!_snapshot.IsMuted));

    public void BeginSeek() { if (CanSeek) _seeking = true; }
    public Task CommitSeekAsync()
    {
        var position = PositionSeconds;
        _seeking = false;
        return CanSeek ? ExecuteAsync(() => _controller.SeekAsync(position)) : Task.CompletedTask;
    }
    public void BeginVolume() { if (CanVolume) _volumeEditing = true; }
    public Task CommitVolumeAsync()
    {
        var value = Volume;
        _volumeEditing = false;
        return CanVolume ? ExecuteAsync(() => _controller.SetVolumeAsync(value)) : Task.CompletedTask;
    }

    private async Task ExecuteAsync(Func<Task<PlaybackCommandResult>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { var result = await action(); Message = result.Succeeded ? "" : result.Message ?? "Não foi possível executar o comando."; }
        catch (Exception exception) { Message = exception.Message; }
        finally { IsBusy = false; }
    }
    private static string FormatTime(double seconds) => TimeSpan.FromSeconds(Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, 864000)).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");
    public void Dispose() { _disposed = true; _controller.Changed -= OnChanged; }
}
