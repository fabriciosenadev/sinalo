using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class HealthDiscoveryConnector(HttpClient httpClient, Func<DateOnly>? operatingDate = null) : IContentDiscoveryConnector
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));
    public ContentSource Source => ContentSource.Health;

    public async Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.Source != ContentSource.Health) throw new InvalidOperationException("Este conector atende somente Minuto de Saúde.");
        var sourceUri = new Uri(configuration.PageUrl);
        var referenceDate = _operatingDate();
        var sourceHtml = await _httpClient.GetStringAsync(sourceUri, cancellationToken);
        var quarterUri = FindQuarterPage(sourceHtml, sourceUri, referenceDate) ?? (IsCurrentQuarterPage(sourceUri, referenceDate) ? sourceUri : null);
        if (quarterUri is null) return [];
        var quarterHtml = quarterUri == sourceUri ? sourceHtml : await _httpClient.GetStringAsync(quarterUri, cancellationToken);
        return ParseVideos(quarterHtml, quarterUri, referenceDate.Year).OrderBy(item => item.ScheduledDate).ToArray();
    }

    private static Uri? FindQuarterPage(string html, Uri sourceUri, DateOnly referenceDate)
    {
        var expectedSlug = $"momento-vida-e-saude-{Quarter.From(referenceDate).Number}trim-{referenceDate.Year}";
        foreach (Match match in LinkPattern.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (!href.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase)) continue;
            if (Uri.TryCreate(sourceUri, href, out var uri) && uri.Scheme == Uri.UriSchemeHttps) return uri;
        }
        return null;
    }

    private static bool IsCurrentQuarterPage(Uri uri, DateOnly referenceDate) => uri.AbsolutePath.Contains($"momento-vida-e-saude-{Quarter.From(referenceDate).Number}trim-{referenceDate.Year}", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ContentItem> ParseVideos(string html, Uri pageUri, int year)
    {
        foreach (Match match in VideoRowPattern.Matches(html))
        {
            var details = DetailsPattern.Match(WebUtility.HtmlDecode(match.Groups["label"].Value));
            if (!details.Success) continue;
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (!Uri.TryCreate(href, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps) continue;
            var date = new DateOnly(year, int.Parse(details.Groups["month"].Value), int.Parse(details.Groups["day"].Value));
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(downloadUri.AbsoluteUri))).ToLowerInvariant()[..16];
            yield return new ContentItem($"health-{date:yyyy-MM-dd}", ContentSource.Health, details.Groups["title"].Value.Trim(), date, pageUri,
                [new MediaAsset($"asset-{hash}", downloadUri, Path.GetFileName(downloadUri.AbsolutePath), ParseSize(WebUtility.HtmlDecode(TagPattern.Replace(match.Groups["row"].Value, " "))), null)]);
        }
    }

    private static long? ParseSize(string text)
    {
        var match = SizePattern.Match(text);
        if (!match.Success) return null;
        var value = decimal.Parse(match.Groups["value"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        return (long)(value * (match.Groups["unit"].Value.Equals("GB", StringComparison.OrdinalIgnoreCase) ? 1024L * 1024 * 1024 : 1024L * 1024));
    }

    private static readonly Regex LinkPattern = new("<a\\b[^>]*\\bhref\\s*=\\s*[\\\"'](?<href>[^\\\"']+)[\\\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex VideoRowPattern = new("(?<row><tr\\b[\\s\\S]*?<span[^>]*\\btitle=[\\\"'](?<label>[^\\\"']+)[\\\"'][\\s\\S]*?<a[^>]*\\bhref=[\\\"'](?<href>https://[^\\\"']+\\.mp4)[^\\\"']*[\\\"'][\\s\\S]*?</tr>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DetailsPattern = new("^\\s*\\d+\\.\\s*(?<day>\\d{2})/(?<month>\\d{2})\\s*-\\s*(?<title>.+?)\\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex SizePattern = new("(?<value>\\d+(?:[.,]\\d+)?)\\s*(?<unit>MB|GB)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.CultureInvariant);
}
