using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sinalo.Infrastructure;

public sealed class ProvaiEVedeDiscoveryConnector(HttpClient httpClient, Func<DateOnly>? operatingDate = null) : IContentDiscoveryConnector
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));
    private static readonly Regex LinkPattern = new("<a\\b[^>]*\\bhref\\s*=\\s*[\\\"'](?<href>[^\\\"']+)[\\\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public ContentSource Source => ContentSource.ProvaiEVede;

    public async Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.Source != ContentSource.ProvaiEVede) throw new InvalidOperationException("Este conector atende somente Provai e Vede.");
        var referenceDate = _operatingDate();
        var html = await _httpClient.GetStringAsync(new Uri(configuration.PageUrl), cancellationToken);
        var target = FindQuarterPage(html, new Uri(configuration.PageUrl), referenceDate);
        if (target is null) return [];

        var quarterHtml = await _httpClient.GetStringAsync(target, cancellationToken);
        var videos = ParseVideos(quarterHtml, referenceDate.Year).ToArray();
        if (videos.Length > 0) return videos;

        var quarter = Quarter.From(referenceDate);
        return
        [
            new ContentItem(
                $"provai-e-vede-{quarter.Year}-t{quarter.Number}",
                ContentSource.ProvaiEVede,
                $"Provai e Vede {quarter.Year} - {quarter.Number}º Trimestre",
                new DateOnly(quarter.Year, ((quarter.Number - 1) * 3) + 1, 1),
                target,
                [])
        ];
    }

    private static Uri? FindQuarterPage(string html, Uri sourceUri, DateOnly referenceDate)
    {
        var expectedSlug = $"provai-e-vede-{referenceDate.Year}-{Quarter.From(referenceDate).Number}o-trimestre";
        foreach (Match match in LinkPattern.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (!href.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase)) continue;
            if (Uri.TryCreate(sourceUri, href, out var quarterUri) && quarterUri.Scheme == Uri.UriSchemeHttps) return quarterUri;
        }

        return null;
    }

    private static IEnumerable<ContentItem> ParseVideos(string html, int year)
    {
        foreach (Match match in DownloadLinkPattern.Matches(html))
        {
            var downloadUri = new Uri(WebUtility.HtmlDecode(match.Groups["href"].Value));
            var rowText = WebUtility.HtmlDecode(TagPattern.Replace(match.Groups["row"].Value, " "));
            var details = VideoDetailsPattern.Match(rowText);
            if (!details.Success) continue;
            var date = new DateOnly(year, int.Parse(details.Groups["month"].Value), int.Parse(details.Groups["day"].Value));
            var title = details.Groups["title"].Value.Trim();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(downloadUri.AbsoluteUri))).ToLowerInvariant()[..16];
            yield return new ContentItem($"provai-e-vede-{date:yyyy-MM-dd}", ContentSource.ProvaiEVede, title, date, downloadUri,
                [new MediaAsset($"asset-{hash}", downloadUri, Path.GetFileName(downloadUri.AbsolutePath), null, null)]);
        }
    }

    private static readonly Regex DownloadLinkPattern = new("(?<row><tr\\b[\\s\\S]*?<a[^>]*href=[\\\"'](?<href>https://[^\\\"']+\\.mp4)[^\\\"']*[\\\"'][^>]*>[\\s\\S]*?</tr>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.CultureInvariant);
    private static readonly Regex VideoDetailsPattern = new("\\((?<day>\\d{2})/(?<month>\\d{2})\\)\\s*(?<title>.*?)\\s*\\d+(?:[.,]\\d+)?\\s*(?:MB|GB)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
