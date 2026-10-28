using Sinalo.Domain;
using System.Text.RegularExpressions;

namespace Sinalo.Application.Synchronization;

public sealed record HealthYouTubeConfiguration(bool Enabled = false,
    string ChannelUrl = "https://www.youtube.com/@VidaeSaudeUCB/videos", string? ChannelId = null,
    string? ChannelName = null, int MaximumHeight = 720, string Revision = "")
{
    public void Validate()
    {
        var uri = NormalizeChannelUrl(ChannelUrl);
        if (ChannelId is not null && uri.AbsolutePath.StartsWith("/channel/", StringComparison.Ordinal) && uri.AbsolutePath.Split('/')[2] != ChannelId)
            throw new ArgumentException("O endereço não corresponde ao canal verificado.");
        if (MaximumHeight is not (480 or 720 or 1080)) throw new ArgumentException("Escolha 480p, 720p ou 1080p.");
        if (Enabled && (!IsChannelId(ChannelId) || string.IsNullOrWhiteSpace(ChannelName)))
            throw new ArgumentException("Verifique o canal antes de habilitar a fonte secundária.");
    }
    public static bool IsChannelId(string? id) => id is not null && Regex.IsMatch(id, "^UC[A-Za-z0-9_-]{22}$");
    public static Uri NormalizeChannelUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host is not ("youtube.com" or "www.youtube.com") || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Informe o endereço HTTPS da aba Vídeos de um canal do YouTube.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!Regex.IsMatch(path, "^/(?:@[A-Za-z0-9_.%-]+|channel/UC[A-Za-z0-9_-]{22})(?:/videos)?$"))
            throw new ArgumentException("Use um canal, não um vídeo, playlist ou pesquisa do YouTube.");
        return new Uri("https://www.youtube.com" + path + (path.EndsWith("/videos", StringComparison.Ordinal) ? "" : "/videos"));
    }
}
public sealed record YouTubeChannelIdentity(string Id, string Name, Uri VideosUrl);
public sealed record ChannelVideo(LinkedVideo Video, string ChannelId, DateTimeOffset? PublishedAt = null, int? Episode = null);
public sealed record ChannelVideoQuery(YouTubeChannelIdentity Channel, IReadOnlyList<ChannelVideo> Videos,
    bool IsLimited, int Excluded, IReadOnlyList<string> Warnings);
public sealed record ChannelVideoApproval(ChannelVideo Candidate, HealthYouTubeConfiguration Configuration, string? BatchId = null);
public sealed class ChannelVideoApprovalRequiredException(string message) : InvalidOperationException(message);

public interface IHealthYouTubeConfigurationService
{
    Task<HealthYouTubeConfiguration> LoadAsync(CancellationToken token = default);
    Task SaveAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default);
}
public interface IChannelVideoService
{
    Task<YouTubeChannelIdentity> VerifyAsync(string url, CancellationToken token = default);
    Task<ChannelVideoQuery> ListRecentAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default);
    Task<ChannelVideo?> InspectAsync(string videoId, HealthYouTubeConfiguration configuration, CancellationToken token = default);
}
public static class ChannelVideoSelection
{
    public static IReadOnlyList<ChannelVideo> Recent(IEnumerable<ChannelVideo> videos) => videos.DistinctBy(x => x.Video.Id)
        .OrderByDescending(x => x.Video.PublishedDate).ThenByDescending(x => x.PublishedAt)
        .ThenByDescending(x => x.Episode).ThenBy(x => x.Video.Id, StringComparer.Ordinal).Take(4).ToArray();
    public static LinkedVideoFormat? Format(LinkedVideo video, int maximumHeight) => video.Formats
        .Where(x => x.Height <= maximumHeight && x.VideoCodec.StartsWith("avc", StringComparison.OrdinalIgnoreCase)
            && x.AudioCodec.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.Height).ThenBy(x => x.EstimatedBytes ?? long.MaxValue).FirstOrDefault();
}
