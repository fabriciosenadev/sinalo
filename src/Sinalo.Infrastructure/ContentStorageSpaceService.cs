using System.Net;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class ContentStorageSpaceService(HttpClient httpClient, ISinaloPathService paths) : IContentStorageSpaceService
{
    private const long OneGigabyte = 1024L * 1024 * 1024;

    public async Task<ContentStorageSpaceAssessment> AssessAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default)
    {
        var knownBytes = 0L;
        var unknownItems = 0;
        foreach (var item in items)
        {
            var asset = item.Assets.SingleOrDefault();
            if (asset is null) continue;
            var size = asset.ExpectedSizeBytes ?? await TryGetContentLengthAsync(asset.DownloadUri, cancellationToken);
            if (size is null)
            {
                unknownItems++;
                continue;
            }

            checked
            {
                // O Informativo pode chegar em ZIP: enquanto extrai, arquivo compactado e vídeo coexistem.
                knownBytes += item.Source == ContentSource.Missions ? size.Value * 2 : size.Value;
            }
        }

        var safetyMargin = Math.Max(OneGigabyte, (long)Math.Ceiling(knownBytes * .25));
        var requiredBytes = checked(knownBytes + safetyMargin);
        var contentPath = paths.GetPaths().ContentPath;
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(contentPath))!);
        return new ContentStorageSpaceAssessment(drive.Name, drive.AvailableFreeSpace, knownBytes, requiredBytes, unknownItems);
    }

    public Task<bool> HasMinimumFreeSpaceAsync(string path, long minimumFreeBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        return Task.FromResult(drive.AvailableFreeSpace >= minimumFreeBytes);
    }

    private async Task<long?> TryGetContentLengthAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented || !response.IsSuccessStatusCode) return null;
            return response.Content.Headers.ContentLength;
        }
        catch (HttpRequestException) { return null; }
    }
}
