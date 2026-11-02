using System.Text.Json;
using Sinalo.Application.LouvorJa;

namespace Sinalo.Infrastructure.LouvorJa;

/// <summary>Protocol subset observed in Louvor JA 2.0.2. An acknowledgement is not execution confirmation.</summary>
public sealed class LouvorJaHttpClient(HttpClient http, Action<string>? diagnostic = null) : ILouvorJaClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(2);

    private async Task<JsonDocument> RequestAsync(string path, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(RequestTimeout);
        diagnostic?.Invoke($"GET {path}");
        try
        {
            using var response = await http.GetAsync(path, deadline.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);
            diagnostic?.Invoke($"HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 1200)]}");
            response.EnsureSuccessStatusCode();
            var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("status", out var status) || status.GetString() != "ok")
            {
                document.Dispose();
                throw new InvalidDataException("Resposta sem confirmação válida do protocolo.");
            }
            return document;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("O Louvor JA excedeu o tempo limite da solicitação."); }
    }

    public async Task ConnectAsync(CancellationToken token = default)
    {
        using var json = await RequestAsync("api/ping", token).ConfigureAwait(false);
        if (json.RootElement.GetProperty("app").GetString() != "LouvorJA" || !json.RootElement.GetProperty("running").GetBoolean())
            throw new InvalidDataException("O destino não corresponde a um Louvor JA disponível.");
    }

    public async Task<IReadOnlyList<LouvorJaSong>> SearchAsync(string query, CancellationToken token = default)
    {
        using var json = await RequestAsync($"api/search-songs?q={Uri.EscapeDataString(query.Trim())}", token).ConfigureAwait(false);
        return json.RootElement.GetProperty("results").Deserialize<LouvorJaSong[]>(JsonOptions)
            ?? throw new InvalidDataException("Catálogo inválido.");
    }

    public async Task<LouvorJaState> GetStateAsync(CancellationToken token = default)
    {
        using var json = await RequestAsync("api/status", token).ConfigureAwait(false);
        var data = json.RootElement.GetProperty("data");
        // The installed protocol may initially publish only { type: "idle", updatedAt: ... }.
        if (data.GetProperty("type").GetString() == "idle") return new("idle", "", "", 0, 0, true, 0);
        if (data.GetProperty("type").GetString() == "bible")
            return new("bible", data.TryGetProperty("bibleReference", out var reference) ? reference.GetString() ?? "Bíblia" : "Bíblia",
                data.TryGetProperty("bibleText", out var text) ? text.GetString() ?? "" : "", 0, 0, true, 0);
        return new(data.GetProperty("type").GetString()!, data.GetProperty("title").GetString()!,
            data.GetProperty("currentLyric").GetString()!, data.GetProperty("slideIndex").GetInt32(),
            data.GetProperty("totalSlides").GetInt32(), data.GetProperty("isPaused").GetBoolean(), data.GetProperty("volume").GetInt32());
    }

    public async Task OpenAsync(int id, LouvorJaMode mode, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        using var json = await RequestAsync($"api/open-song?id={id}&tag={(int)mode}", token).ConfigureAwait(false);
    }

    public async Task SendAsync(LouvorJaCommand command, int volume = 50, CancellationToken token = default)
    {
        var path = command switch
        {
            LouvorJaCommand.TogglePlayback => "api/play-pause",
            LouvorJaCommand.NextSlide => "api/song-slides?action=next",
            LouvorJaCommand.PreviousSlide => "api/song-slides?action=previous",
            LouvorJaCommand.Close => "api/close-media?force=true",
            LouvorJaCommand.Volume when volume is >= 0 and <= 100 => $"api/volume?value={volume}",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        using var json = await RequestAsync(path, token).ConfigureAwait(false);
    }
}
