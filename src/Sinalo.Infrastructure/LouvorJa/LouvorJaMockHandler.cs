using System.Net;
using System.Text;
using System.Text.Json;
using Sinalo.Application.LouvorJa;

namespace Sinalo.Infrastructure.LouvorJa;

public enum LouvorJaScenario { Normal, Unavailable, Slow, Empty, InvalidResponse, NoEffect, Disconnect }

/// <summary>Terminal HTTP handler: never forwards requests to sockets, processes or the installed app.</summary>
public sealed class LouvorJaMockHandler : HttpMessageHandler
{
    private static readonly LouvorJaSong[] Catalog =
    [new(9001, "Canção de teste — Esperança", "Catálogo fictício", true),
     new(9002, "Canção de teste — Gratidão", "Catálogo fictício", false),
     new(9003, "Canção de teste — Comunhão", "Catálogo fictício", true)];
    private LouvorJaState _state = Idle;
    private bool _disconnected;
    private static LouvorJaState Idle => new("idle", "", "", 0, 0, true, 50);
    public LouvorJaScenario Scenario { get; set; }
    public TimeSpan Latency { get; init; } = TimeSpan.FromMilliseconds(180);
    public void Reset() { _state = Idle; _disconnected = false; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        await Task.Delay(Scenario == LouvorJaScenario.Slow ? TimeSpan.FromSeconds(10) : Latency, token).ConfigureAwait(false);
        if (Scenario == LouvorJaScenario.Unavailable || _disconnected) throw new HttpRequestException("Simulador indisponível/desconectado.");
        if (Scenario == LouvorJaScenario.InvalidResponse) return new(HttpStatusCode.OK) { Content = new StringContent("{resposta-inválida") };
        var uri = request.RequestUri!;
        var parameters = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x.ElementAtOrDefault(1) ?? ""));
        string Parameter(string name) => parameters.GetValueOrDefault(name, "");
        object response;
        switch (uri.AbsolutePath)
        {
            case "/api/ping": response = new { status = "ok", app = "LouvorJA", running = true, tokenRequired = false }; break;
            case "/api/status": response = new { status = "ok", data = new { type = _state.Type, title = _state.Title, currentLyric = _state.CurrentLyric, slideIndex = _state.SlideIndex, totalSlides = _state.TotalSlides, isPaused = _state.IsPaused, volume = _state.Volume } }; break;
            case "/api/search-songs":
                var found = Scenario == LouvorJaScenario.Empty || string.IsNullOrWhiteSpace(Parameter("q")) ? [] : Catalog.Where(x => x.Name.Contains(Parameter("q"), StringComparison.OrdinalIgnoreCase)).ToArray();
                response = new { status = "ok", musicas = found, results = found }; break;
            case "/api/open-song":
            case "/api/play-pause":
            case "/api/song-slides":
            case "/api/close-media":
            case "/api/volume":
                if (Scenario == LouvorJaScenario.Disconnect) _disconnected = true;
                else if (Scenario != LouvorJaScenario.NoEffect)
                {
                    switch (uri.AbsolutePath)
                    {
                        case "/api/open-song":
                            var song = Catalog.Single(x => x.Id == int.Parse(Parameter("id")));
                            _state = new("media", song.Name, "Slide fictício 1 — nenhuma projeção real", 0, 3, Parameter("tag") == "3", _state.Volume); break;
                        case "/api/play-pause": _state = _state with { IsPaused = !_state.IsPaused }; break;
                        case "/api/close-media": _state = Idle with { Volume = _state.Volume }; break;
                        case "/api/volume": _state = _state with { Volume = Math.Clamp(int.Parse(Parameter("value")), 0, 100) }; break;
                        case "/api/song-slides":
                            var slide = Math.Clamp(_state.SlideIndex + (Parameter("action") == "next" ? 1 : -1), 0, Math.Max(0, _state.TotalSlides - 1));
                            _state = _state with { SlideIndex = slide, CurrentLyric = $"Slide fictício {slide + 1} — nenhuma projeção real" }; break;
                    }
                }
                response = new { status = "ok" }; break;
            default: return new(HttpStatusCode.NotFound) { Content = new StringContent("Rota não simulada.") };
        }
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json") };
    }
}
