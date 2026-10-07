using System.Diagnostics;
using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class WindowsPlaybackLauncher : IPlaybackLauncher, IPlaybackController
{
    public event Action<bool>? PlaybackActivityChanged;
    public PlaybackSnapshot Current { get; private set; } = PlaybackSnapshot.Idle;
    public event Action<PlaybackSnapshot>? Changed;
    public event Action<PlaybackSnapshot>? Ended;
    public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vlcPath = FindVlcPath();
            var process = string.IsNullOrWhiteSpace(vlcPath)
                ? Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true, Verb = "open" })
                : Process.Start(new ProcessStartInfo { FileName = vlcPath, Arguments = BuildVlcArguments(filePath, options), UseShellExecute = false });

            if (process is not null)
            {
                var sessionId = Guid.NewGuid();
                Current = new(sessionId, options.ContentId, options.Title ?? Path.GetFileNameWithoutExtension(filePath),
                    filePath, string.IsNullOrWhiteSpace(vlcPath) ? "Aplicativo padrão" : "VLC", options, PlaybackState.Playing);
                Changed?.Invoke(Current);
                PlaybackActivityChanged?.Invoke(true);
                var completed = 0;
                void OnExited(object? _, EventArgs __)
                {
                    if (Interlocked.Exchange(ref completed, 1) != 0) return;
                    if (Current.SessionId == sessionId)
                    {
                        Current = Current with { State = PlaybackState.Ended, EndReason = PlaybackEndReason.ExternalClosed };
                        Changed?.Invoke(Current);
                        Ended?.Invoke(Current);
                    }
                    PlaybackActivityChanged?.Invoke(false);
                    process.Dispose();
                }
                process.Exited += OnExited;
                process.EnableRaisingEvents = true;
                if (process.HasExited) OnExited(process, EventArgs.Empty);
            }

            var result = CreateLaunchResult(vlcPath, process is not null);
            return Task.FromResult(result with
            {
                Message = result.Started && !string.IsNullOrWhiteSpace(vlcPath)
                    ? $"Vídeo aberto no VLC em {options.OutputLabel}."
                    : result.Message
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Task.FromResult(new PlaybackLaunchResult(false, string.Empty, "Não foi possível abrir o vídeo no VLC ou no aplicativo padrão do Windows.")); }
    }

    public Task<PlaybackCommandResult> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) => Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> StopMediaAsync(CancellationToken cancellationToken = default) => Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> RestartAsync(CancellationToken cancellationToken = default) => Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> SeekAsync(double seconds, CancellationToken cancellationToken = default) => Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => Task.FromResult(PlaybackCommandResult.Unsupported);
    public Task<PlaybackCommandResult> SetMutedAsync(bool muted, CancellationToken cancellationToken = default) => Task.FromResult(PlaybackCommandResult.Unsupported);

    public static PlaybackLaunchResult CreateLaunchResult(string? vlcPath, bool processStarted) =>
        !processStarted ? new PlaybackLaunchResult(false, string.Empty, "O Windows não conseguiu iniciar um player para este vídeo.") :
        string.IsNullOrWhiteSpace(vlcPath) ? new PlaybackLaunchResult(true, "Aplicativo padrão", "VLC não encontrado; vídeo aberto no aplicativo padrão do Windows.") :
        new PlaybackLaunchResult(true, "VLC", "Vídeo aberto no VLC.");

    public static string BuildVlcArguments(string filePath, PlaybackLaunchOptions options) =>
        $"\"{filePath}\" --fullscreen --qt-fullscreen-screennumber={options.PlayerScreenIndex}";

    public static string? FindVlcPath()
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC", "vlc.exe")
        };
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        candidates.AddRange(pathEntries.Select(entry => Path.Combine(entry.Trim(), "vlc.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }
}
