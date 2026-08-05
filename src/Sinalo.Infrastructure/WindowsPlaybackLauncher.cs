using System.Diagnostics;
using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class WindowsPlaybackLauncher : IPlaybackLauncher
{
    public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vlcPath = FindVlcPath();
            var process = string.IsNullOrWhiteSpace(vlcPath)
                ? Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true, Verb = "open" })
                : Process.Start(new ProcessStartInfo { FileName = vlcPath, Arguments = BuildVlcArguments(filePath, options), UseShellExecute = false });

            return Task.FromResult(CreateLaunchResult(vlcPath, process is not null));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Task.FromResult(new PlaybackLaunchResult(false, string.Empty, "Não foi possível abrir o vídeo no VLC ou no aplicativo padrão do Windows.")); }
    }

    public static PlaybackLaunchResult CreateLaunchResult(string? vlcPath, bool processStarted) =>
        !processStarted ? new PlaybackLaunchResult(false, string.Empty, "O Windows não conseguiu iniciar um player para este vídeo.") :
        string.IsNullOrWhiteSpace(vlcPath) ? new PlaybackLaunchResult(true, "Aplicativo padrão", "VLC não encontrado; vídeo aberto no aplicativo padrão do Windows.") :
        new PlaybackLaunchResult(true, "VLC", "Vídeo aberto no VLC.");

    public static string BuildVlcArguments(string filePath, PlaybackLaunchOptions? options) =>
        options?.FullscreenScreenNumber is > 0
            ? $"\"{filePath}\" --fullscreen --qt-fullscreen-screennumber={options.FullscreenScreenNumber}"
            : $"\"{filePath}\"";

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
