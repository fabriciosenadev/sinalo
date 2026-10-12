using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class MpvPlaybackLauncher : IPlaybackLauncher, IPlaybackPreloader, IPlaybackController, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _mpvPath;
    private readonly string? _configuredPipeName;
    private readonly TimeSpan _commandTimeout;
    private readonly TimeSpan _loadTimeout;
    private Process? _process;
    private MpvIpcConnection? _ipc;
    private PlaybackSnapshot _current = PlaybackSnapshot.Idle;
    private TaskCompletionSource? _loaded;
    private TaskCompletionSource? _ended;
    private long? _entryId;
    private bool _acceptStart;
    private bool _isLoaded;
    private bool _stoppingProcess;
    private PlaybackEndReason? _expectedEnd;
    private long _lastPositionNotification;
    private int _disposed;
    public PlaybackSnapshot Current { get { lock (_stateGate) return _current; } }
    public event Action<PlaybackSnapshot>? Changed;
    public event Action<PlaybackSnapshot>? Ended;
    public event Action<bool>? PlaybackActivityChanged;
    public int? ProcessId => _process is { HasExited: false } ? _process.Id : null;

    public MpvPlaybackLauncher(string? mpvPath = null, string? pipeName = null,
        TimeSpan? commandTimeout = null, TimeSpan? loadTimeout = null)
    {
        _mpvPath = mpvPath ?? GetDefaultMpvPath();
        _configuredPipeName = pipeName;
        _commandTimeout = commandTimeout ?? TimeSpan.FromSeconds(3);
        _loadTimeout = loadTimeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try { await EnsureStartedAsync(linked.Token).ConfigureAwait(false); }
        catch { await CloseProcessAsync().ConfigureAwait(false); throw; }
        finally { _gate.Release(); }
    }

    public async Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            try { await EnsureStartedAsync(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { await CloseProcessAsync().ConfigureAwait(false); throw; }
            catch (Exception exception)
            {
                await CloseProcessAsync().ConfigureAwait(false);
                Publish(Current with { State = PlaybackState.Failed, Error = exception.Message, EndReason = PlaybackEndReason.Error });
                return new(false, "MPV", "Não foi possível iniciar o player rápido do Sinalo.", AllowsFallback: true);
            }
            if (Current.IsActive)
            {
                try { await StopCoreAsync(PlaybackEndReason.Replaced, linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    await CloseProcessAsync().ConfigureAwait(false);
                    Finish(_lifetime.IsCancellationRequested ? PlaybackEndReason.ApplicationShutdown : PlaybackEndReason.Stopped);
                    throw;
                }
                catch (Exception exception)
                {
                    await CloseProcessAsync().ConfigureAwait(false);
                    Finish(PlaybackEndReason.Error, $"Não foi possível substituir o vídeo: {exception.Message}");
                    return new(false, "MPV", Current.Error!, false);
                }
            }
            _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _entryId = null;
            _isLoaded = false;
            _acceptStart = true;
            _expectedEnd = null;
            Publish(new(Guid.NewGuid(), options.ContentId, options.Title ?? Path.GetFileNameWithoutExtension(filePath),
                filePath, "MPV", options, PlaybackState.Loading, Volume: Current.Volume, IsMuted: Current.IsMuted,
                Capabilities: PlaybackCapabilities.All));
            try
            {
                await SendAsync(["set_property", "pause", false], linked.Token).ConfigureAwait(false);
                await SendAsync(["set_property", "volume", Current.Volume], linked.Token).ConfigureAwait(false);
                await SendAsync(["set_property", "mute", Current.IsMuted], linked.Token).ConfigureAwait(false);
                await SendAsync(["set_property", "fullscreen", false], linked.Token).ConfigureAwait(false);
                await SendAsync(["set_property", "fs-screen", options.PlayerScreenIndex], linked.Token).ConfigureAwait(false);
                await SendAsync(["loadfile", filePath, "replace"], linked.Token).ConfigureAwait(false);
                await _loaded.Task.WaitAsync(_loadTimeout, linked.Token).ConfigureAwait(false);
                await RefreshPropertiesAsync(linked.Token).ConfigureAwait(false);
                await PositionWindowOnOutputAsync(options, linked.Token).ConfigureAwait(false);
                await SendAsync(["set_property", "fullscreen", true], linked.Token).ConfigureAwait(false);
                return new(true, "MPV", $"Vídeo aberto no player rápido do Sinalo em {options.OutputLabel}.");
            }
            catch (OperationCanceledException)
            {
                await CloseProcessAsync().ConfigureAwait(false);
                Finish(_lifetime.IsCancellationRequested ? PlaybackEndReason.ApplicationShutdown : PlaybackEndReason.Stopped);
                throw;
            }
            catch (Exception exception)
            {
                await CloseProcessAsync().ConfigureAwait(false);
                Finish(PlaybackEndReason.Error, $"Não foi possível carregar o vídeo: {exception.Message}");
                return new(false, "MPV", Current.Error!, AllowsFallback: false);
            }
        }
        finally { _gate.Release(); }
    }

    public Task<PlaybackCommandResult> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) => ControlAsync(async token =>
    {
        await SendAsync(["set_property", "pause", paused], token).ConfigureAwait(false);
        await RefreshPropertyAsync("pause", token).ConfigureAwait(false);
    }, cancellationToken);
    public Task<PlaybackCommandResult> StopMediaAsync(CancellationToken cancellationToken = default) =>
        ControlAsync(token => StopCoreAsync(PlaybackEndReason.Stopped, token), cancellationToken);
    public Task<PlaybackCommandResult> RestartAsync(CancellationToken cancellationToken = default) => ControlAsync(async token =>
    {
        await SendAsync(["seek", 0, "absolute+exact"], token).ConfigureAwait(false);
        await SendAsync(["set_property", "pause", false], token).ConfigureAwait(false);
        await RefreshPropertyAsync("pause", token).ConfigureAwait(false);
        await RefreshPropertyAsync("time-pos", token).ConfigureAwait(false);
    }, cancellationToken);
    public Task<PlaybackCommandResult> SeekAsync(double seconds, CancellationToken cancellationToken = default) => ControlAsync(async token =>
    {
        if (!double.IsFinite(seconds) || Current.DurationSeconds is not > 0) throw new InvalidOperationException("A duração deste vídeo ainda não está disponível.");
        await SendAsync(["seek", Math.Clamp(seconds, 0, Current.DurationSeconds.Value), "absolute+exact"], token).ConfigureAwait(false);
        await RefreshPropertyAsync("time-pos", token).ConfigureAwait(false);
    }, cancellationToken);
    public Task<PlaybackCommandResult> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => ControlAsync(async token =>
    {
        if (!double.IsFinite(volume)) throw new InvalidOperationException("Volume inválido.");
        await SendAsync(["set_property", "volume", Math.Clamp(volume, 0, 100)], token).ConfigureAwait(false);
        await RefreshPropertyAsync("volume", token).ConfigureAwait(false);
    }, cancellationToken);
    public Task<PlaybackCommandResult> SetMutedAsync(bool muted, CancellationToken cancellationToken = default) => ControlAsync(async token =>
    {
        await SendAsync(["set_property", "mute", muted], token).ConfigureAwait(false);
        await RefreshPropertyAsync("mute", token).ConfigureAwait(false);
    }, cancellationToken);

    private async Task<PlaybackCommandResult> ControlAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var session = Current.SessionId;
        if (Volatile.Read(ref _disposed) != 0) return new(false, "O player foi encerrado.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (Current.SessionId != session || Current.State is not (PlaybackState.Playing or PlaybackState.Paused))
                return new(false, "Não há vídeo ativo para este comando.");
            try { await action(linked.Token).ConfigureAwait(false); return PlaybackCommandResult.Success; }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                if (_ipc?.IsConnected != true) OnDisconnected();
                return new(false, exception.Message);
            }
        }
        finally { _gate.Release(); }
    }
    private Task<JsonElement> SendAsync(object[] command, CancellationToken token) =>
        (_ipc ?? throw new IOException("Canal do MPV indisponível.")).SendAsync(command, token);

    private async Task EnsureStartedAsync(CancellationToken token)
    {
        if (_process is { HasExited: false } && _ipc?.IsConnected == true) return;
        await CloseProcessAsync().ConfigureAwait(false);
        if (!File.Exists(_mpvPath)) throw new FileNotFoundException("MPV não encontrado no pacote do Sinalo.", _mpvPath);
        var pipeName = _configuredPipeName ?? $"sinalo-mpv-{Guid.NewGuid():N}";
        _process = Process.Start(new ProcessStartInfo { FileName = _mpvPath, Arguments = BuildStartArguments(pipeName), UseShellExecute = false, CreateNoWindow = true })
            ?? throw new IOException("O processo MPV não foi iniciado.");
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try { await pipe.ConnectAsync(5000, token).ConfigureAwait(false); }
        catch { pipe.Dispose(); throw; }
        _ipc = new(pipe, _commandTimeout);
        _ipc.EventReceived += HandleEvent;
        _ipc.Disconnected += OnDisconnected;
        var properties = new[] { "time-pos", "duration", "pause", "volume", "mute", "path" };
        for (var index = 0; index < properties.Length; index++)
            await SendAsync(["observe_property", index + 1, properties[index]], token).ConfigureAwait(false);
    }
    private async Task RefreshPropertiesAsync(CancellationToken token)
    {
        foreach (var property in new[] { "duration", "time-pos", "pause", "volume", "mute", "path" })
            await RefreshPropertyAsync(property, token).ConfigureAwait(false);
    }
    private async Task RefreshPropertyAsync(string property, CancellationToken token)
    {
        try { ApplyProperty(property, await SendAsync(["get_property", property], token).ConfigureAwait(false), force: true); }
        catch (MpvCommandException) when (property is "duration" or "time-pos") { }
    }

    private void HandleEvent(JsonElement message)
    {
        var name = message.GetProperty("event").GetString();
        if (name == "start-file" && _acceptStart)
        {
            if (!message.TryGetProperty("playlist_entry_id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var entry)) return;
            _entryId = entry;
            _acceptStart = false;
        }
        else if (name == "file-loaded" && _entryId is not null)
        {
            _isLoaded = true;
            Publish(Current with { State = PlaybackState.Playing });
            _loaded?.TrySetResult();
        }
        else if (name == "property-change" && message.TryGetProperty("name", out var property) && property.ValueKind == JsonValueKind.String && message.TryGetProperty("data", out var data))
            ApplyProperty(property.GetString(), data);
        else if (name == "end-file")
        {
            if (message.TryGetProperty("playlist_entry_id", out var id) && (id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var entry) || _entryId != entry)) return;
            var reason = message.TryGetProperty("reason", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : "unknown";
            var endReason = _expectedEnd ?? (reason == "eof" ? PlaybackEndReason.Natural : reason == "error" ? PlaybackEndReason.Error : PlaybackEndReason.ExternalClosed);
            if (!_isLoaded && _loaded is not null)
            {
                _loaded.TrySetException(new IOException("O arquivo não pôde ser carregado pelo MPV."));
                _ = _loaded.Task.Exception;
            }
            _isLoaded = false;
            if (endReason != PlaybackEndReason.Replaced) Finish(endReason, endReason == PlaybackEndReason.Error ? "O MPV encontrou um erro na reprodução do arquivo." : null);
            else Ended?.Invoke(Current with { State = PlaybackState.Ended, EndReason = endReason });
            _ended?.TrySetResult();
        }
    }
    private void ApplyProperty(string? name, JsonElement value, bool force = false)
    {
        if (!_isLoaded || !Current.IsActive) return;
        var current = Current;
        var next = current;
        if (name == "pause" && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            next = current with { State = value.GetBoolean() ? PlaybackState.Paused : PlaybackState.Playing };
        else if (name == "mute" && value.ValueKind is JsonValueKind.True or JsonValueKind.False) next = current with { IsMuted = value.GetBoolean() };
        else if (name == "path" && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } path)
        {
            var sameFile = string.Equals(current.FilePath?.Replace('/', '\\'), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
            next = current with { FilePath = path, ContentId = sameFile ? current.ContentId : null, Title = sameFile ? current.Title : Path.GetFileNameWithoutExtension(path) };
        }
        else if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number))
            next = name switch
            {
                "time-pos" => current with { PositionSeconds = Math.Max(0, number) },
                "duration" => current with { DurationSeconds = number > 0 ? number : null },
                "volume" => current with { Volume = Math.Clamp(number, 0, 100) },
                _ => current
            };
        if (next == current) return;
        var now = Environment.TickCount64;
        var notify = force || name != "time-pos" || now - _lastPositionNotification >= 250;
        if (notify && name == "time-pos") _lastPositionNotification = now;
        Publish(next, notify);
    }
    private void Publish(PlaybackSnapshot snapshot, bool notify = true)
    {
        bool previousActive;
        lock (_stateGate) { previousActive = _current.IsActive; _current = snapshot; }
        if (previousActive != snapshot.IsActive) PlaybackActivityChanged?.Invoke(snapshot.IsActive);
        if (notify) Changed?.Invoke(snapshot);
    }
    private void Finish(PlaybackEndReason reason, string? error = null)
    {
        var previous = Current;
        if (previous.EndReason is not null && !previous.IsActive) return;
        var snapshot = previous with { State = reason == PlaybackEndReason.Error ? PlaybackState.Failed : PlaybackState.Ended, EndReason = reason, Error = error };
        Publish(snapshot);
        Ended?.Invoke(snapshot);
    }
    private void OnDisconnected()
    {
        if (_stoppingProcess) return;
        _isLoaded = false;
        if (Current.State == PlaybackState.Loading) _loaded?.TrySetException(new IOException("A conexão com o MPV foi perdida."));
        if (_expectedEnd is not null) _ended?.TrySetException(new IOException("A conexão com o MPV foi perdida."));
        if (Current.IsActive) Finish(PlaybackEndReason.ExternalClosed, "O player foi fechado ou perdeu a conexão. Abra o vídeo novamente.");
    }
    private async Task StopCoreAsync(PlaybackEndReason reason, CancellationToken token)
    {
        _expectedEnd = reason;
        await SendAsync(["stop"], token).ConfigureAwait(false);
        if (_ended is not null) await _ended.Task.WaitAsync(_commandTimeout, token).ConfigureAwait(false);
    }

    public static string GetDefaultMpvPath() => Path.Combine(AppContext.BaseDirectory, "binaries", "mpv", "mpv.exe");
    public static string BuildStartArguments(string pipeName) => string.Join(' ',
        ["--player-operation-mode=cplayer", "--idle=yes", "--force-window=no", "--keep-open=no", "--terminal=no", "--really-quiet", "--no-border", "--title-bar=no", "--hwdec=auto-safe", "--hwdec-software-fallback=3", "--no-config", "--no-resume-playback", $"--input-ipc-server=\\\\.\\pipe\\{pipeName}", "--title=\"Sinalo Player\""]);

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async Task PositionWindowOnOutputAsync(PlaybackLaunchOptions options, CancellationToken token)
    {
        if (!options.HasOutputBounds || _process is null) return;
        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < timeoutAt)
        {
            token.ThrowIfCancellationRequested();
            _process.Refresh();
            if (_process.MainWindowHandle != IntPtr.Zero)
            {
                SetWindowPos(_process.MainWindowHandle, IntPtr.Zero, options.BoundsX!.Value, options.BoundsY!.Value,
                    options.BoundsWidth!.Value, options.BoundsHeight!.Value, 0x0004 | 0x0010 | 0x0040);
                return;
            }
            await Task.Delay(25, token).ConfigureAwait(false);
        }
    }
    // Kept for compatibility with existing callers/tests; session state uses the detailed events above.
    public static bool? ParsePlaybackActivityEvent(string line)
    {
        try
        {
            using var response = JsonDocument.Parse(line);
            if (response.RootElement.ValueKind != JsonValueKind.Object || !response.RootElement.TryGetProperty("event", out var name) || name.ValueKind != JsonValueKind.String) return null;
            return name.GetString() switch { "file-loaded" => true, "end-file" or "idle" => false, _ => null };
        }
        catch (JsonException) { return null; }
    }
    private async Task CloseProcessAsync()
    {
        _stoppingProcess = true;
        try
        {
            if (_ipc is not null)
            {
                _ipc.EventReceived -= HandleEvent;
                _ipc.Disconnected -= OnDisconnected;
                try { await _ipc.SendAsync(["quit"]).ConfigureAwait(false); }
                catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException or ObjectDisposedException) { }
                await _ipc.DisposeAsync().ConfigureAwait(false);
                _ipc = null;
            }
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                        catch (TimeoutException) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                    }
                }
                catch (InvalidOperationException) { }
                finally { _process.Dispose(); _process = null; }
            }
        }
        finally { _stoppingProcess = false; _isLoaded = false; }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CloseProcessAsync().ConfigureAwait(false);
            if (Current.IsActive) Finish(PlaybackEndReason.ApplicationShutdown);
        }
        finally { _gate.Release(); }
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
