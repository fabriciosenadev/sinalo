using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sinalo.Application.Synchronization;

namespace Sinalo.Infrastructure;

/// <summary>Metadata only. Downloading remains a separate, explicitly approved operation.</summary>
public sealed class ChannelVideoService : IChannelVideoService
{
    private readonly VideoToolRunner _runner;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, (DateTimeOffset At, ChannelVideo Video)> _cache = new();
    private readonly SemaphoreSlim _queryGate = new(1, 1);
    public ChannelVideoService(string? toolDirectory = null, VideoToolRunner? runner = null, Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _runner = runner ?? ((args, output, token) => LinkedVideoService.RunProcessAsync(
            toolDirectory ?? Path.Combine(AppContext.BaseDirectory, "binaries", "video-download"), args, output, token));
    }
    private async Task<string> RunAsync(IReadOnlyList<string> arguments, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        VideoToolResult result;
        try { result = await _runner(arguments, null, timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("O YouTube não respondeu a tempo. Tente novamente."); }
        if (result.ExitCode != 0) throw new IOException("Não foi possível consultar o YouTube. Verifique a conexão e a disponibilidade do canal/vídeo.");
        if (result.Output.Length > 16 * 1024 * 1024) throw new InvalidDataException("A consulta ultrapassou o limite de metadados.");
        return result.Output;
    }
    private Task<string> ListJsonAsync(Uri uri, int limit, CancellationToken token) => RunAsync(
        ["--flat-playlist", "--skip-download", "--dump-single-json", "--playlist-end", limit.ToString(CultureInfo.InvariantCulture), "--socket-timeout", "20", "--retries", "1", uri.AbsoluteUri], token);

    public async Task<YouTubeChannelIdentity> VerifyAsync(string url, CancellationToken token = default)
    {
        var uri = HealthYouTubeConfiguration.NormalizeChannelUrl(url);
        using var document = JsonDocument.Parse(await ListJsonAsync(uri, 1, token).ConfigureAwait(false));
        return Identity(document.RootElement);
    }
    private static YouTubeChannelIdentity Identity(JsonElement root)
    {
        var id = Text(root, "channel_id");
        var name = Text(root, "channel");
        if (!HealthYouTubeConfiguration.IsChannelId(id) || string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("O endereço não retornou a identidade de um canal válido.");
        return new(id!, name, new Uri($"https://www.youtube.com/channel/{id}/videos"));
    }
    public async Task<ChannelVideoQuery> ListRecentAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default)
    {
        configuration.Validate();
        if (!configuration.Enabled) throw new InvalidOperationException("Habilite e verifique a fonte secundária nas configurações.");
        var caller = token;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        token = deadline.Token;
        await _queryGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(await ListJsonAsync(new Uri($"https://www.youtube.com/channel/{configuration.ChannelId}/videos"), 100, token).ConfigureAwait(false));
            var root = document.RootElement;
            var channel = Identity(root);
            if (channel.Id != configuration.ChannelId) throw new InvalidDataException("O canal retornado não corresponde ao canal verificado. Verifique novamente nas configurações.");
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("A lista de vídeos não foi reconhecida.");
            var videos = new List<ChannelVideo>();
            var warnings = new List<string>();
            var excluded = 0;
            var incomplete = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            DateOnly? previousDate = null;
            var orderChanged = false;
            var inspected = 0;
            foreach (var entry in entries.EnumerateArray().Take(100))
            {
                token.ThrowIfCancellationRequested();
                var id = entry.ValueKind == JsonValueKind.Object ? Text(entry, "id") : null;
                if (id is null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$") || !seen.Add(id)) { excluded++; continue; }
                if (Text(entry, "live_status") is "is_upcoming" or "is_live" or "post_live") { excluded++; continue; }
                ChannelVideo? video;
                try
                {
                    if (_cache.TryGetValue(id, out var cached) && _clock() - cached.At < TimeSpan.FromMinutes(15) && cached.Video.ChannelId == channel.Id)
                        video = ChannelVideoSelection.Format(cached.Video.Video, configuration.MaximumHeight) is null ? null : cached.Video;
                    else
                    {
                        video = await InspectAsync(id, configuration, token).ConfigureAwait(false);
                        if (video is not null) { if (_cache.Count >= 100) _cache.Clear(); _cache[id] = (_clock(), video); }
                    }
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or TimeoutException or JsonException)
                {
                    warnings.Add("Um candidato não pôde ser conferido; o resultado é parcial. Atualize para tentar novamente.");
                    incomplete = true;
                    break;
                }
                inspected++;
                if (video is null) { excluded++; incomplete = true; continue; }
                if (previousDate is { } previous && video.Video.PublishedDate > previous) orderChanged = true;
                previousDate = video.Video.PublishedDate;
                videos.Add(video);
                // The channel's /videos feed is newest-first, but scheduled premieres are interleaved.
                // Inspect a further published boundary item; if order is inconsistent, inspect the bounded window.
                if (videos.Count >= 5 && !orderChanged) break;
            }
            var reachedLimit = seen.Count >= 100 || orderChanged;
            if (reachedLimit) warnings.Add("Consulta limitada a 100 entradas do canal; a ordem retornada exige revisão. Não foi varrido todo o histórico.");
            if (inspected == 0 && entries.GetArrayLength() >= 100) reachedLimit = true;
            return new(channel, ChannelVideoSelection.Recent(videos), incomplete || reachedLimit, excluded, warnings.Distinct().ToArray());
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        { throw new TimeoutException("A consulta do canal atingiu o limite de cinco minutos. Tente novamente quando a conexão melhorar."); }
        finally { _queryGate.Release(); }
    }
    public async Task<ChannelVideo?> InspectAsync(string videoId, HealthYouTubeConfiguration configuration, CancellationToken token = default)
    {
        if (!Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$")) throw new ArgumentException("Identificador de vídeo inválido.");
        var json = await RunAsync(["--dump-single-json", "--skip-download", "--no-playlist", "--socket-timeout", "20", "--retries", "1", $"https://www.youtube.com/watch?v={videoId}"], token).ConfigureAwait(false);
        var video = ParseVideo(json, configuration, _clock());
        if (video is not null && video.Video.Id != videoId) throw new InvalidDataException("O vídeo retornado não corresponde ao selecionado.");
        return video;
    }
    public static ChannelVideo? ParseVideo(string json, HealthYouTubeConfiguration configuration, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (Text(root, "channel_id") != configuration.ChannelId) throw new InvalidDataException("O vídeo não pertence ao canal verificado.");
        if (Text(root, "availability") != "public" || Text(root, "live_status") is not ("not_live" or "was_live")) return null;
        DateTimeOffset? published = null;
        foreach (var property in new[] { "release_timestamp", "timestamp" })
            if (root.TryGetProperty(property, out var timestamp) && timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var seconds))
            { published = DateTimeOffset.FromUnixTimeSeconds(seconds); break; }
        if (published > now) return null;
        DateOnly date;
        if (published is { } instant) date = DateOnly.FromDateTime(instant.UtcDateTime);
        else if (!DateOnly.TryParseExact(Text(root, "upload_date"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return null;
        if (date > DateOnly.FromDateTime(now.UtcDateTime)) return null;
        var id = Text(root, "id");
        if (id is null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$")) return null;
        LinkedVideo video;
        try { video = LinkedVideoService.ParseMetadata(json, new Uri($"https://www.youtube.com/watch?v={id}"), compatibleOnly: true) with { PublishedDate = date }; }
        catch (InvalidDataException) { return null; }
        if (ChannelVideoSelection.Format(video, configuration.MaximumHeight) is null) return null;
        var match = Regex.Match(video.Title, @"\b(?:EP|EPIS[ÓO]DIO)[\s.:-]*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        int? episode = match.Success && int.TryParse(match.Groups[1].Value, out var number) ? number : null;
        return new(video, configuration.ChannelId!, published, episode);
    }
    private static string? Text(JsonElement value, string property) => value.TryGetProperty(property, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
}
