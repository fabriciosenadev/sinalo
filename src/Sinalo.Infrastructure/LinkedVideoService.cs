using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Application.Playback;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

/// <summary>Runs the packaged downloader outside the UI process. Partial files are deliberately kept on failure.</summary>
public sealed record VideoToolResult(int ExitCode, string Output, string Error);
public delegate Task<VideoToolResult> VideoToolRunner(IReadOnlyList<string> arguments, Action<string>? onOutput, CancellationToken cancellationToken);

public sealed class LinkedVideoService(ISinaloPathService paths, string? toolDirectory = null, PlaybackActivityGate? playbackGate = null, VideoToolRunner? toolRunner = null, Func<TimeSpan, CancellationToken, Task>? retryDelay = null, Func<string, long>? freeSpace = null) : ILinkedVideoService
{
    private static readonly Regex ProgressPattern = new(@"(?<percent>\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly string _toolDirectory = toolDirectory ?? Path.Combine(AppContext.BaseDirectory, "binaries", "video-download");
    private readonly PlaybackActivityGate? _playbackGate = playbackGate;
    private readonly VideoToolRunner _runner = toolRunner ?? ((arguments, onOutput, token) => RunProcessAsync(toolDirectory ?? Path.Combine(AppContext.BaseDirectory, "binaries", "video-download"), arguments, onOutput, token));
    private readonly Func<TimeSpan, CancellationToken, Task> _retryDelay = retryDelay ?? Task.Delay;
    private readonly Func<string, long> _freeSpace = freeSpace ?? (path => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace);

    public async Task<LinkedVideo> InspectAsync(string url, CancellationToken cancellationToken = default)
    {
        var page = ValidateUrl(url);
        var result = await _runner(["--dump-single-json", "--skip-download", "--no-playlist", page.AbsoluteUri], null, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException(FriendlyError(result.Error));
        return ParseMetadata(result.Output, page);
    }

    public async Task<ContentItem> DownloadAsync(LinkedVideoDownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var video = request.Video;
        if (!Regex.IsMatch(video.Id, "^[A-Za-z0-9_-]{11}$")) throw new InvalidDataException("Identificador de vídeo inválido.");
        if (!video.Formats.Contains(request.Format)) throw new InvalidOperationException("A qualidade escolhida não pertence ao vídeo consultado.");
        paths.EnsureFolders();
        var contentRoot = paths.GetPaths().ContentPath;
        var itemId = request.ItemId;
        var targetDirectory = Path.Combine(contentRoot, Quarter.From(request.ScheduledDate).ToString(), LinkedVideoDownloadRequest.SourceDirectory(request.Destination));
        var target = Path.Combine(targetDirectory, $"youtube-{video.Id}.mp4");
        var partialDirectory = Path.Combine(contentRoot, ".incoming", itemId + "-" + request.Format.VideoFormatId);
        var free = _freeSpace(contentRoot);
        var required = Math.Max(512L * 1024 * 1024, (request.Format.EstimatedBytes ?? 0) * 3 + 128L * 1024 * 1024);
        if (free < required) throw new IOException($"Espaço insuficiente para baixar e preparar este vídeo. Libere pelo menos {required / 1024 / 1024} MB.");
        Directory.CreateDirectory(partialDirectory);
        var pending = new ContentItem(itemId, request.Destination, video.Title, request.ScheduledDate, video.PageUri, [], SyncState.Downloading, true);
        var outputTemplate = Path.Combine(partialDirectory, "%(id)s.%(ext)s");
        var args = new List<string>
        {
            "--no-playlist", "--continue", "--part", "--newline", "--retries", "5", "--fragment-retries", "5",
            "--retry-sleep", "http:exp=2:30", "--retry-sleep", "fragment:exp=2:30",
            "--abort-on-unavailable-fragments", "--format", request.Format.Selector,
            "--merge-output-format", "mp4", "--output", outputTemplate, video.PageUri.AbsoluteUri
        };
        var ffmpeg = Path.Combine(_toolDirectory, "ffmpeg.exe");
        if (request.Format.AudioFormatId is not null && !File.Exists(ffmpeg))
            throw new FileNotFoundException("O componente de conversão de vídeo não está instalado.", ffmpeg);
        if (File.Exists(ffmpeg)) { args.Insert(0, _toolDirectory); args.Insert(0, "--ffmpeg-location"); }

        progress?.Report(new DownloadProgress(pending, 0, request.Format.EstimatedBytes, "Baixando"));
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 3;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_playbackGate is not null) await _playbackGate.WaitUntilIdleAsync(cancellationToken);
            using var running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var pausedForPlayback = false;
            void OnPlaybackChanged(bool active) { if (active) { pausedForPlayback = true; running.Cancel(); } }
            if (_playbackGate is not null) _playbackGate.Changed += OnPlaybackChanged;
            if (_playbackGate?.IsActive == true) OnPlaybackChanged(true);
            VideoToolResult result;
            try { result = await _runner(args, line =>
            {
                var match = ProgressPattern.Match(line);
                var percentage = match.Success && double.TryParse(match.Groups["percent"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
                progress?.Report(new DownloadProgress(pending, (long)Math.Round(percentage), 100, "Baixando"));
            }, running.Token); }
            catch (OperationCanceledException) when (pausedForPlayback && !cancellationToken.IsCancellationRequested)
            {
                progress?.Report(new DownloadProgress(pending, 0, null, "Pausado durante a reprodução"));
                continue;
            }
            finally { if (_playbackGate is not null) _playbackGate.Changed -= OnPlaybackChanged; }
            if (result.ExitCode == 0) { lastError = null; break; }
            lastError = new IOException(FriendlyError(result.Error));
            if (attempt < 3)
            {
                progress?.Report(new DownloadProgress(pending, 0, request.Format.EstimatedBytes, $"Conexão interrompida. Nova tentativa {attempt + 1}/3"));
                await _retryDelay(TimeSpan.FromSeconds(attempt * 5), cancellationToken);
            }
            attempt++;
        }
        if (lastError is not null) throw lastError;

        var completed = Directory.EnumerateFiles(partialDirectory, "*.mp4", SearchOption.TopDirectoryOnly)
            .Where(file => !file.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => new FileInfo(file).Length).FirstOrDefault();
        if (completed is null) throw new InvalidDataException("O download terminou sem produzir um arquivo MP4.");
        progress?.Report(new DownloadProgress(pending, 0, null, "Validando arquivo"));
        try { await ValidateMp4Async(completed, cancellationToken); }
        catch (InvalidDataException) { File.Delete(completed); throw; }
        var length = new FileInfo(completed).Length;
        string hash;
        await using (var input = File.OpenRead(completed)) hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
        Directory.CreateDirectory(targetDirectory);
        File.Move(completed, target, true);
        var asset = new MediaAsset(itemId + "-mp4", video.PageUri, Path.GetFileName(target), length, hash);
        var ready = pending with { Assets = [asset], SyncState = SyncState.Ready, LocalPath = target };
        progress?.Report(new DownloadProgress(ready, length, length, "Disponível offline"));
        try { Directory.Delete(partialDirectory, true); } catch (IOException) { }
        return ready;
    }

    public static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            (uri.Host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be")))
            throw new ArgumentException("Cole um link HTTPS de vídeo do YouTube.");
        return uri;
    }

    public static LinkedVideo ParseMetadata(string json, Uri page)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$")) throw new InvalidDataException("O link não aponta para um vídeo válido.");
        var title = root.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : null;
        if (string.IsNullOrWhiteSpace(title)) title = $"Vídeo {id}";
        var date = DateOnly.FromDateTime(DateTime.Today);
        if (root.TryGetProperty("upload_date", out var upload) && DateOnly.TryParseExact(upload.GetString(), "yyyyMMdd", out var uploaded)) date = uploaded;
        var formats = root.GetProperty("formats").EnumerateArray().ToArray();
        var audio = formats.Where(format => String(format, "ext") == "m4a" && String(format, "acodec") != "none")
            .OrderByDescending(format => Number(format, "abr")).FirstOrDefault();
        var audioId = audio.ValueKind == JsonValueKind.Undefined ? null : String(audio, "format_id");
        var options = formats
            .Where(format => String(format, "ext") == "mp4" && String(format, "vcodec") != "none" && Number(format, "height") > 0)
            .Select(format => new LinkedVideoFormat(
                String(format, "format_id") ?? "", String(format, "acodec") == "none" ? audioId : null,
                (int)Number(format, "height"), Long(format, "filesize") ?? Long(format, "filesize_approx")))
            .Where(format => format.VideoFormatId.Length > 0 && (format.AudioFormatId is not null || formats.Any(f => String(f, "format_id") == format.VideoFormatId && String(f, "acodec") != "none")))
            .GroupBy(format => format.Height).Select(group => group.OrderByDescending(format => format.EstimatedBytes).First())
            .OrderBy(format => format.Height).ToArray();
        if (options.Length == 0) throw new InvalidDataException("Nenhuma qualidade MP4 com áudio compatível foi encontrada para este vídeo.");
        return new LinkedVideo(id, title, page, date, options);
    }

    // External executable boundary: business decisions are tested with an injected runner.
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static async Task<VideoToolResult> RunProcessAsync(string toolDirectory, IReadOnlyList<string> args, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        var executable = Path.Combine(toolDirectory, "yt-dlp.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("O componente de download não está instalado. Reinstale o Sinalo.", executable);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add("--ignore-config");
        foreach (var arg in args) start.ArgumentList.Add(arg);
        var deno = Path.Combine(toolDirectory, "deno.exe");
        if (File.Exists(deno)) { start.ArgumentList.Insert(1, "--js-runtimes"); start.ArgumentList.Insert(2, "deno:" + deno); }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível iniciar o componente de download.");
        using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        var output = new StringBuilder();
        var errors = new StringBuilder();
        var outputTask = ReadLinesAsync(process.StandardOutput, line => { if (onOutput is null) output.AppendLine(line); else onOutput(line); });
        var errorTask = ReadLinesAsync(process.StandardError, line => { if (errors.Length < 8192) errors.AppendLine(line); });
        await process.WaitForExitAsync();
        await Task.WhenAll(outputTask, errorTask);
        cancellationToken.ThrowIfCancellationRequested();
        return new VideoToolResult(process.ExitCode, output.ToString(), errors.ToString());
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static async Task ReadLinesAsync(StreamReader reader, Action<string> consume)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null) consume(line);
    }

    private static async Task ValidateMp4Async(string path, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length < 1024) throw new InvalidDataException("O arquivo de vídeo está incompleto.");
        var header = new byte[12];
        await using var input = File.OpenRead(path);
        if (await input.ReadAsync(header, cancellationToken) != header.Length || Encoding.ASCII.GetString(header, 4, 4) != "ftyp")
            throw new InvalidDataException("O arquivo baixado não é um MP4 válido.");
    }

    private static string? String(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static double Number(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
    private static long? Long(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;
    private static string FriendlyError(string error) => string.IsNullOrWhiteSpace(error) ? "Não foi possível obter o vídeo. Tente novamente quando a conexão melhorar." : error.Trim().Split('\n').Last().Trim();
}
