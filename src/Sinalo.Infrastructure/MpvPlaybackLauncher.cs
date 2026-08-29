using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class MpvPlaybackLauncher : IPlaybackLauncher, IPlaybackPreloader, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _mpvPath;
    private readonly string? _configuredPipeName;
    private string _pipeName = string.Empty;
    private Process? _process;
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readerCancellation;

    public MpvPlaybackLauncher(string? mpvPath = null, string? pipeName = null)
    {
        _mpvPath = mpvPath ?? GetDefaultMpvPath();
        _configuredPipeName = pipeName;
    }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await EnsureStartedAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await EnsureStartedAsync(cancellationToken);
                await SendCommandAsync(["set_property", "fullscreen", false], cancellationToken);
                await SendCommandAsync(["set_property", "fs-screen", options.PlayerScreenIndex], cancellationToken);

                await SendCommandAsync(["loadfile", filePath, "replace"], cancellationToken);
                await PositionWindowOnOutputAsync(options, cancellationToken);
                await SendCommandAsync(["set_property", "fullscreen", true], cancellationToken);

                return new PlaybackLaunchResult(true, "MPV", $"Vídeo aberto no player rápido do Sinalo em {options.OutputLabel}.");
            }
            finally { _gate.Release(); }
        }
        catch (OperationCanceledException) { throw; }
        catch { return new PlaybackLaunchResult(false, string.Empty, "Não foi possível abrir o player rápido do Sinalo."); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { await StopAsync(); }
        finally { _gate.Release(); _gate.Dispose(); }
    }

    public static string GetDefaultMpvPath() => Path.Combine(AppContext.BaseDirectory, "binaries", "mpv", "mpv.exe");

    public static string BuildStartArguments(string pipeName) => string.Join(' ',
    [
        "--player-operation-mode=cplayer", "--idle=yes", "--force-window=no", "--keep-open=no", "--terminal=no", "--really-quiet", "--no-border", "--title-bar=no", "--hwdec=auto-safe", "--hwdec-software-fallback=3", $"--input-ipc-server=\\\\.\\pipe\\{pipeName}", "--title=Sinalo Player"
    ]);

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false } && _pipe is { IsConnected: true } && _writer is not null) return;
        await StopAsync();
        if (!File.Exists(_mpvPath)) throw new FileNotFoundException("MPV não encontrado no pacote do Sinalo.", _mpvPath);

        _pipeName = _configuredPipeName ?? $"sinalo-mpv-{Guid.NewGuid():N}";
        _process = Process.Start(new ProcessStartInfo
        {
            FileName = _mpvPath,
            Arguments = BuildStartArguments(_pipeName),
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("O processo MPV não foi iniciado.");

        _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipe.ConnectAsync(5000, cancellationToken);
        _writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        _readerCancellation = new CancellationTokenSource();
        _ = DrainResponsesAsync(_pipe, _readerCancellation.Token);
    }

    private async Task SendCommandAsync(object[] command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_writer is null) throw new InvalidOperationException("Canal do MPV indisponível.");
        await _writer.WriteLineAsync(JsonSerializer.Serialize(new { command }));
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async Task PositionWindowOnOutputAsync(PlaybackLaunchOptions options, CancellationToken cancellationToken)
    {
        if (!options.HasOutputBounds || _process is null) return;

        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _process.Refresh();
            var windowHandle = _process.MainWindowHandle;
            if (windowHandle != IntPtr.Zero)
            {
                SetWindowPos(
                    windowHandle,
                    IntPtr.Zero,
                    options.BoundsX!.Value,
                    options.BoundsY!.Value,
                    options.BoundsWidth!.Value,
                    options.BoundsHeight!.Value,
                    NoZOrder | NoActivate | ShowWindow);
                return;
            }

            await Task.Delay(25, cancellationToken);
        }
    }

    private static async Task DrainResponsesAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            while (await reader.ReadLineAsync(cancellationToken) is not null) { }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private async Task StopAsync()
    {
        _readerCancellation?.Cancel();
        _readerCancellation?.Dispose();
        _readerCancellation = null;
        if (_writer is not null)
        {
            try { await _writer.WriteLineAsync(JsonSerializer.Serialize(new { command = new[] { "quit" } })); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }

            try { _writer.Dispose(); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }

            _writer = null;
        }

        try { _pipe?.Dispose(); }
        catch (IOException) { }
        catch (ObjectDisposedException) { }

        _pipe = null;
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }

        _process?.Dispose();
        _process = null;
    }

    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint ShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
