using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Application.Synchronization;
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
        var sourceUri = new Uri(configuration.PageUrl);
        if (sourceUri.Scheme != Uri.UriSchemeHttps) throw new SiteStructureChangedException("Use um endereço HTTPS para Provai e Vede.");
        var html = await HttpSynchronizationRetry.GetStringAsync(_httpClient, sourceUri, cancellationToken);
        var directPage = GetQuarterPage(sourceUri);
        var libras = sourceUri.Fragment.Equals("#libras", StringComparison.OrdinalIgnoreCase);
        var target = directPage is not null ? sourceUri : FindQuarterPage(html, sourceUri, referenceDate, libras);
        if (target is null) throw new SiteStructureChangedException(libras
            ? "A página do trimestre atual em Libras não foi encontrada no site oficial."
            : "A página trimestral não foi encontrada no site oficial.");

        var page = GetQuarterPage(target)!.Value;
        var quarterHtml = directPage is not null ? html : await HttpSynchronizationRetry.GetStringAsync(_httpClient, target, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var videos = ParseVideos(quarterHtml, page.Quarter, page.Libras).DistinctBy(item => item.Id).ToArray();
        if (videos.Length > 0) return videos;

        var quarter = page.Quarter;
        var variant = page.Libras ? "-libras" : "";
        return
        [
            new ContentItem(
                $"provai-e-vede{variant}-{quarter.Year}-t{quarter.Number}",
                ContentSource.ProvaiEVede,
                $"Provai e Vede {quarter.Year} - {quarter.Number}º Trimestre{(page.Libras ? " (Libras)" : "")}",
                new DateOnly(quarter.Year, ((quarter.Number - 1) * 3) + 1, 1),
                target,
                [])
        ];
    }

    private static Uri? FindQuarterPage(string html, Uri sourceUri, DateOnly referenceDate, bool libras)
    {
        var expectedQuarter = Quarter.From(referenceDate);
        foreach (Match match in LinkPattern.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (Uri.TryCreate(sourceUri, href, out var quarterUri) &&
                quarterUri.Scheme == Uri.UriSchemeHttps &&
                GetQuarterPage(quarterUri) is { } page && page.Libras == libras && page.Quarter == expectedQuarter) return quarterUri;
        }

        return null;
    }

    private static (Quarter Quarter, bool Libras)? GetQuarterPage(Uri uri)
    {
        var match = QuarterPagePattern.Match(uri.AbsolutePath);
        if (!match.Success) return null;
        var year = int.Parse(match.Groups["year"].Value);
        return year == 0 ? null : (new Quarter(year, int.Parse(match.Groups["quarter"].Value)), match.Groups["libras"].Success);
    }

    private static IEnumerable<ContentItem> ParseVideos(string html, Quarter quarter, bool libras)
    {
        foreach (Match row in RowPattern.Matches(html))
        {
            var match = DownloadLinkPattern.Match(row.Value);
            if (!match.Success) continue;
            var downloadUri = new Uri(WebUtility.HtmlDecode(match.Groups["href"].Value));
            var rowText = WebUtility.HtmlDecode(TagPattern.Replace(row.Value, " "));
            var details = VideoDetailsPattern.Match(rowText);
            if (!details.Success) continue;
            var month = int.Parse(details.Groups["month"].Value);
            var day = int.Parse(details.Groups["day"].Value);
            if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(quarter.Year, month)) continue;
            var date = new DateOnly(quarter.Year, month, day);
            if (Quarter.From(date) != quarter) continue;
            var title = details.Groups["title"].Value.Trim() + (libras ? " (Libras)" : "");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(downloadUri.AbsoluteUri))).ToLowerInvariant()[..16];
            var variant = libras ? "-libras" : "";
            yield return new ContentItem($"provai-e-vede{variant}-{date:yyyy-MM-dd}", ContentSource.ProvaiEVede, title, date, downloadUri,
                [new MediaAsset($"asset-{hash}", downloadUri, (libras ? "libras-" : "") + Path.GetFileName(downloadUri.AbsolutePath), null, null)]);
        }
    }

    private static readonly Regex QuarterPagePattern = new("/provai-e-vede-(?<year>[0-9]{4})-(?<quarter>[1-4])o-trimestre(?<libras>-libras)?/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RowPattern = new("<tr\\b[^>]*>[\\s\\S]*?</tr>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DownloadLinkPattern = new("<a[^>]*href=[\\\"'](?<href>https://[^\\\"']+\\.mp4)[^\\\"']*[\\\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.CultureInvariant);
    private static readonly Regex VideoDetailsPattern = new("\\((?<day>\\d{2})/(?<month>\\d{2})\\)\\s*(?<title>.*?)\\s*\\d+(?:[.,]\\d+)?\\s*(?:MB|GB)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
