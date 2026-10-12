using System.Diagnostics;
using System.IO;

namespace Sinalo.Tests.Integration;

internal sealed class PlaybackVideoFixture : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"), "valid.mp4");
    public static async Task<PlaybackVideoFixture> CreateAsync(double duration = 8)
    {
        var fixture = new PlaybackVideoFixture();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fixture.Path)!);
        var start = new ProcessStartInfo(System.IO.Path.Combine(AppContext.BaseDirectory, "binaries", "video-download", "ffmpeg.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=black:s=64x64:r=10", "-t", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:v", "mpeg4", "-y", fixture.Path }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(process.ExitCode == 0, await error);
        return fixture;
    }
    public void Dispose() { if (Directory.Exists(System.IO.Path.GetDirectoryName(Path))) Directory.Delete(System.IO.Path.GetDirectoryName(Path)!, true); }
}
