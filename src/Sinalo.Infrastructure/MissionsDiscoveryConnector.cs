using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class MissionsDiscoveryConnector(HttpClient httpClient, Func<DateOnly>? operatingDate = null) : IContentDiscoveryConnector
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly Func<DateOnly> _operatingDate = operatingDate ?? (() => DateOnly.FromDateTime(DateTime.Today));

    public ContentSource Source => ContentSource.Missions;

    public async Task<IReadOnlyList<ContentItem>> DiscoverAsync(SourceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.Source != ContentSource.Missions) throw new InvalidOperationException("Este conector atende somente Informativo das Missões.");

        var sourceUri = new Uri(configuration.PageUrl);
        var referenceDate = _operatingDate();
        var sourceHtml = await _httpClient.GetStringAsync(sourceUri, cancellationToken);
        var quarterUri = FindQuarterPage(sourceHtml, sourceUri, referenceDate);
        if (quarterUri is null) return [];

        var quarterHtml = await _httpClient.GetStringAsync(quarterUri, cancellationToken);
        var posts = FindMissionPosts(quarterHtml, quarterUri, referenceDate.Year).ToArray();
        if (posts.Length == 0)
        {
            var quarter = Quarter.From(referenceDate);
            return [new ContentItem($"missions-{quarter.Year}-t{quarter.Number}", ContentSource.Missions,
                $"Informativo das Missões {quarter.Year} - {quarter.Number}º Trimestre", new DateOnly(quarter.Year, ((quarter.Number - 1) * 3) + 1, 1), quarterUri, [])];
        }

        var items = new List<ContentItem>();
        foreach (var post in posts)
        {
            var postHtml = await _httpClient.GetStringAsync(post.Uri, cancellationToken);
            var downloadUri = FindVideoDownload(postHtml, post.Uri);
            IReadOnlyList<MediaAsset> assets = downloadUri is null ? [] : [CreateAsset(downloadUri)];
            items.Add(new ContentItem($"missions-{post.Date:yyyy-MM-dd}", ContentSource.Missions, post.Title, post.Date, post.Uri, assets,
                downloadUri is null ? SyncState.OnlineOnly : SyncState.Pending));
        }

        return items.OrderBy(item => item.ScheduledDate).ToArray();
    }

    private static Uri? FindQuarterPage(string html, Uri sourceUri, DateOnly referenceDate)
    {
        var quarter = Quarter.From(referenceDate);
        var expected = $"{quarter.Number}º trimestre {quarter.Year}";
        return FindAnchors(html, sourceUri)
            .Where(anchor => Normalize(anchor.Text).Contains(expected, StringComparison.OrdinalIgnoreCase))
            .Select(anchor => anchor.Uri)
            .FirstOrDefault(uri => uri.Scheme == Uri.UriSchemeHttps);
    }

    private static IEnumerable<MissionPost> FindMissionPosts(string html, Uri sourceUri, int year)
    {
        foreach (var anchor in FindAnchors(html, sourceUri))
        {
            var title = Normalize(anchor.Text);
            if (!title.StartsWith("Informativo Mundial das Missões", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Alternativo", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Infantil", StringComparison.OrdinalIgnoreCase) ||
                !TryParseMissionDate(title, year, out var date)) continue;

            yield return new MissionPost(anchor.Uri, date, title);
        }
    }

    private static Uri? FindVideoDownload(string html, Uri postUri) => FindAnchors(html, postUri)
        .Select(anchor => anchor.Uri)
        .FirstOrDefault(uri => uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));

    private static MediaAsset CreateAsset(Uri downloadUri)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(downloadUri.AbsoluteUri))).ToLowerInvariant()[..16];
        var fileName = Path.GetFileName(downloadUri.AbsolutePath);
        return new MediaAsset($"asset-{hash}", downloadUri, fileName, null, null);
    }

    private static bool TryParseMissionDate(string title, int expectedYear, out DateOnly date)
    {
        var match = DatePattern.Match(Normalize(title));
        if (!match.Success || !Months.TryGetValue(match.Groups["month"].Value.ToUpperInvariant(), out var month))
        {
            date = default;
            return false;
        }

        var year = match.Groups["year"].Success ? int.Parse(match.Groups["year"].Value) : expectedYear;
        date = new DateOnly(year, month, int.Parse(match.Groups["day"].Value));
        return true;
    }

    private static IEnumerable<Anchor> FindAnchors(string html, Uri baseUri)
    {
        foreach (Match match in AnchorPattern.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (Uri.TryCreate(baseUri, href, out var uri)) yield return new Anchor(uri, WebUtility.HtmlDecode(TagPattern.Replace(match.Groups["text"].Value, " ")));
        }
    }

    private static string Normalize(string value) => Regex.Replace(WebUtility.HtmlDecode(value), "\\s+", " ").Trim();

    private sealed record Anchor(Uri Uri, string Text);
    private sealed record MissionPost(Uri Uri, DateOnly Date, string Title);

    private static readonly Regex AnchorPattern = new("<a\\b[^>]*\\bhref\\s*=\\s*[\\\"'](?<href>[^\\\"']+)[\\\"'][^>]*>(?<text>[\\s\\S]*?)</a>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.CultureInvariant);
    private static readonly Regex DatePattern = new("(?<day>\\d{1,2})\\s+(?<month>JANEIRO|FEVEREIRO|MARÇO|MARCO|ABRIL|MAIO|JUNHO|JULHO|AGOSTO|SETEMBRO|OUTUBRO|NOVEMBRO|DEZEMBRO)(?:\\s+(?<year>\\d{4}))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, int> Months = new Dictionary<string, int>
    {
        ["JANEIRO"] = 1, ["FEVEREIRO"] = 2, ["MARÇO"] = 3, ["MARCO"] = 3, ["ABRIL"] = 4, ["MAIO"] = 5, ["JUNHO"] = 6,
        ["JULHO"] = 7, ["AGOSTO"] = 8, ["SETEMBRO"] = 9, ["OUTUBRO"] = 10, ["NOVEMBRO"] = 11, ["DEZEMBRO"] = 12
    };
}
