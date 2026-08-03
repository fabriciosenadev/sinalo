using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed partial class OfficialFileDiscoveryConnector(ContentSource source, HttpClient httpClient) : IContentDiscoveryConnector
{
    private readonly HttpClient _httpClient = httpClient;
    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".mov", ".m4v"];

    public ContentSource Source { get; } = source;

    public async Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var pageUri = new Uri(configuration.PageUrl);
        var html = await _httpClient.GetStringAsync(pageUri, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);

        return AnchorHrefRegex().Matches(html)
            .Select(match => match.Groups["href"].Value)
            .Select(href => Uri.TryCreate(pageUri, WebUtility.HtmlDecode(href), out var uri) ? uri : null)
            .Where(uri => uri is not null && uri.Scheme == Uri.UriSchemeHttps && IsDirectVideo(uri))
            .DistinctBy(uri => uri!.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Select(uri => CreateItem(configuration.Source, uri!, today))
            .ToArray();
    }

    private static bool IsDirectVideo(Uri uri) => VideoExtensions.Any(extension => uri.AbsolutePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static ContentItem CreateItem(ContentSource source, Uri downloadUri, DateOnly scheduledDate)
    {
        var fileName = Path.GetFileName(downloadUri.AbsolutePath);
        var idHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(downloadUri.AbsoluteUri))).ToLowerInvariant()[..16];
        return new ContentItem(
            $"{source.ToString().ToLowerInvariant()}-{idHash}", source, fileName, scheduledDate, downloadUri,
            [new MediaAsset($"asset-{idHash}", downloadUri, fileName, null, null)]);
    }

    [GeneratedRegex("href\\s*=\\s*[\\\"'](?<href>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorHrefRegex();
}
