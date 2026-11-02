using System.Text.Json.Serialization;

namespace Sinalo.Application.LouvorJa;

public sealed record LouvorJaSong(
    [property: JsonPropertyName("id_music")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("has_instrumental")] bool HasInstrumental);

public sealed record LouvorJaState(string Type, string Title, string CurrentLyric, int SlideIndex,
    int TotalSlides, bool IsPaused, int Volume);

public enum LouvorJaMode { Sung = 1, Instrumental = 2, Silent = 3 }
public enum LouvorJaCommand { TogglePlayback, NextSlide, PreviousSlide, Close, Volume }

public interface ILouvorJaClient
{
    Task ConnectAsync(CancellationToken token = default);
    Task<IReadOnlyList<LouvorJaSong>> SearchAsync(string query, CancellationToken token = default);
    Task<LouvorJaState> GetStateAsync(CancellationToken token = default);
    Task OpenAsync(int id, LouvorJaMode mode, CancellationToken token = default);
    Task SendAsync(LouvorJaCommand command, int volume = 50, CancellationToken token = default);
}
