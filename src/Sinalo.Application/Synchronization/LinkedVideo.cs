using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public sealed record LinkedVideoFormat(string VideoFormatId, string? AudioFormatId, int Height, long? EstimatedBytes)
{
    public string Label => $"{Height}p MP4" + (EstimatedBytes is > 0 ? $" · aproximadamente {EstimatedBytes / 1024 / 1024} MB" : "");
    public string Selector => AudioFormatId is null ? VideoFormatId : $"{VideoFormatId}+{AudioFormatId}";
    public override string ToString() => Label;
}

public sealed record LinkedVideo(string Id, string Title, Uri PageUri, DateOnly PublishedDate, IReadOnlyList<LinkedVideoFormat> Formats);

public sealed record LinkedVideoDownloadRequest(LinkedVideo Video, LinkedVideoFormat Format, ContentSource Destination, DateOnly ScheduledDate)
{
    public string ItemId => $"{SourceDirectory(Destination)}-youtube-{Video.Id}";
    public static bool IsManualItemId(string id) =>
        id.StartsWith("health-youtube-", StringComparison.Ordinal) ||
        id.StartsWith("missions-youtube-", StringComparison.Ordinal) ||
        id.StartsWith("provai-e-vede-youtube-", StringComparison.Ordinal);

    public static string SourceDirectory(ContentSource source) => source switch
    {
        ContentSource.Missions => "missions",
        ContentSource.ProvaiEVede => "provai-e-vede",
        ContentSource.Health => "health",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}

public interface ILinkedVideoService
{
    Task<LinkedVideo> InspectAsync(string url, CancellationToken cancellationToken = default);
    Task<ContentItem> DownloadAsync(LinkedVideoDownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
