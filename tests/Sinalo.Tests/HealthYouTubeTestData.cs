using System.Text.Json;
using Sinalo.Application.Library;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.Tests;
internal static class HealthYouTubeTestData
{
    public const string ChannelId = "UCufCWU_e8Q_ONyBgf90fZdA";
    public static readonly DateTimeOffset Now = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);
    public static HealthYouTubeConfiguration Configuration => new(true, $"https://www.youtube.com/channel/{ChannelId}/videos", ChannelId, "Vida e Saúde UCB", 720, "revision");
    public static string Id(int number) => $"video{number:000000}";
    public static string Metadata(int number, string state = "not_live", string availability = "public", string? date = "20261021", string channel = ChannelId, long? timestamp = null, string codec = "avc1.64001f", int height = 720, string? title = null) => JsonSerializer.Serialize(new
    {
        id = Id(number), title = title ?? $"EP {number} - Saúde", channel_id = channel, live_status = state,
        availability, upload_date = date, release_timestamp = timestamp,
        formats = new[] { new { format_id = "18", ext = "mp4", vcodec = codec, acodec = "mp4a.40.2", height, filesize = 4096 } }
    });
    public static ChannelVideo Video(int number, string date = "20261021") => Sinalo.Infrastructure.ChannelVideoService.ParseVideo(Metadata(number, date: date), Configuration, Now)!;
    public static LinkedVideoDownloadRequest Request(ChannelVideo? candidate = null, HealthYouTubeConfiguration? config = null)
    {
        var video = candidate ?? Video(1);
        return new(video.Video, video.Video.Formats[0], ContentSource.Health, video.Video.PublishedDate,
            CategoryId: "health", LibraryStorage: true, ChannelApproval: new(video, config ?? Configuration));
    }
    public sealed class Settings : IHealthYouTubeConfigurationService
    {
        public HealthYouTubeConfiguration Current { get; set; } = Configuration;
        public Task<HealthYouTubeConfiguration> LoadAsync(CancellationToken token = default) => Task.FromResult(Current);
        public Task SaveAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default) { Current = configuration; return Task.CompletedTask; }
    }
    public sealed class Channel : IChannelVideoService
    {
        public ChannelVideo? Current { get; set; } = Video(1);
        public IReadOnlyList<ChannelVideo> Videos { get; set; } = [Video(1), Video(2), Video(3), Video(4)];
        public bool Fail { get; set; }
        public Func<string, ChannelVideo?>? Inspector { get; set; }
        public Task<YouTubeChannelIdentity> VerifyAsync(string url, CancellationToken token = default) => Task.FromResult(new YouTubeChannelIdentity(ChannelId, "Vida e Saúde UCB", new(Configuration.ChannelUrl)));
        public Task<ChannelVideoQuery> ListRecentAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (Fail) throw new System.IO.IOException("Teste de rede");
            return Task.FromResult(new ChannelVideoQuery(new(ChannelId, "Vida e Saúde UCB", new(configuration.ChannelUrl)), Videos, false, 0, []));
        }
        public Task<ChannelVideo?> InspectAsync(string id, HealthYouTubeConfiguration configuration, CancellationToken token = default) => Task.FromResult(Inspector is null ? Current : Inspector(id));
    }
    public sealed class Categories : IMediaCategoryService
    {
        public event Action? Changed { add { } remove { } }
        public event Action<IReadOnlyList<string>>? MediaRemoved { add { } remove { } }
        public LibraryMedia? Existing { get; set; }
        public LinkedVideoDownloadRequest? Published { get; private set; }
        public Task<LibraryMedia?> FindLinkedAnywhereAsync(string videoId, CancellationToken token = default) => Task.FromResult(Existing);
        public Task<LibraryMedia?> FindLinkedAsync(string videoId, string categoryId, CancellationToken token = default) => Task.FromResult(Existing?.CategoryId == categoryId ? Existing : null);
        public Task PublishLinkedAsync(LinkedVideoDownloadRequest request, ContentItem downloaded, CancellationToken token = default) { Published = request; return Task.CompletedTask; }
        public Task ValidateDestinationAsync(string id, CancellationToken token = default) => Task.CompletedTask;
        public Task<IReadOnlyList<MediaCategory>> ListAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<MediaCategory>>([]);
        public Task<MediaCategory> CreateAsync(string name, CancellationToken token = default) => throw new NotSupportedException();
        public Task RenameAsync(MediaCategory category, string name, CancellationToken token = default) => throw new NotSupportedException();
        public Task ReorderAsync(string id, int direction, CancellationToken token = default) => throw new NotSupportedException();
        public Task AssignAsync(IReadOnlyList<string> ids, string categoryId, CancellationToken token = default) => throw new NotSupportedException();
        public Task<CategoryImpact> InspectAsync(string id, CancellationToken token = default) => throw new NotSupportedException();
        public Task RemoveAsync(MediaCategory category, string? transferTo, CancellationToken token = default, IReadOnlyList<string>? expectedMediaIds = null) => throw new NotSupportedException();
    }
}
