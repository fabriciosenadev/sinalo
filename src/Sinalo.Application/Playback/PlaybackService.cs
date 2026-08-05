using Sinalo.Application.Catalog;
using Sinalo.Domain;

namespace Sinalo.Application.Playback;

public sealed record PlaybackResult(bool Started, string Message, ContentItem? Item = null);

public sealed class PlaybackService(IContentCatalog catalog, IPlaybackLauncher launcher)
{
    public async Task<PlaybackResult> PlayAsync(string contentItemId, PlaybackLaunchOptions? options = null, CancellationToken cancellationToken = default)
    {
        var item = await catalog.FindByIdAsync(contentItemId, cancellationToken);
        if (item is null) return new(false, "O conteúdo não foi encontrado no catálogo local.");
        if (!item.IsReadyOffline || string.IsNullOrWhiteSpace(item.LocalPath)) return new(false, "Este vídeo ainda não está pronto para reprodução offline.", item);
        if (!File.Exists(item.LocalPath)) return new(false, "O arquivo local do vídeo não foi encontrado.", item);

        var launch = await launcher.LaunchAsync(item.LocalPath, options, cancellationToken);
        if (!launch.Started) return new(false, launch.Message, item);

        var playedAt = DateTimeOffset.UtcNow;
        await catalog.RecordPlaybackAsync(item.Id, playedAt, cancellationToken);
        var played = item with
        {
            PlayCount = item.PlayCount + 1,
            FirstPlayedAtUtc = item.FirstPlayedAtUtc ?? playedAt,
            LastPlayedAtUtc = playedAt
        };
        return new(true, launch.Message, played);
    }
}
