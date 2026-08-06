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
        var discovered = new HashSet<Uri>();
        // Na página trimestral atual, o texto do link é somente "Download". O título e a data
        // pertencem ao parágrafo que o envolve, portanto precisam ser lidos em conjunto.
        foreach (Match match in MissionPostParagraphPattern.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (!Uri.TryCreate(sourceUri, href, out var uri) || !discovered.Add(uri)) continue;

            var context = Normalize(TagPattern.Replace(match.Groups["content"].Value, " "));
            if (!TryParseMissionDate(context, year, out var date)) continue;
            yield return new MissionPost(uri, date, ExtractMissionTitle(context, date));
        }

        foreach (var anchor in FindAnchors(html, sourceUri))
        {
            if (!discovered.Add(anchor.Uri)) continue;
            var title = Normalize(anchor.Text);
            if (!title.StartsWith("Informativo Mundial das Missões", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Alternativo", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Infantil", StringComparison.OrdinalIgnoreCase) ||
                !TryParseMissionDate(title, year, out var date)) continue;

            yield return new MissionPost(anchor.Uri, date, title);
        }
    }

    private static Uri? FindVideoDownload(string html, Uri postUri)
    {
        var directFile = FindAnchors(html, postUri)
            .Select(anchor => anchor.Uri)
            .FirstOrDefault(uri => uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));
        if (directFile is not null) return directFile;

        // O contador-download do site devolve ZIP por uma URL dinâmica, sem extensão no href.
        // O texto do link é a informação estável: escolhemos o vídeo em português de maior resolução.
        return FindAnchors(html, postUri)
            .Where(anchor => anchor.Uri.Scheme == Uri.UriSchemeHttps &&
                             anchor.Text.Contains("Download: Vídeo em Português", StringComparison.OrdinalIgnoreCase))
            .Select(anchor => new { anchor.Uri, Resolution = GetResolution(anchor.Text) })
            .OrderByDescending(candidate => candidate.Resolution)
            .Select(candidate => candidate.Uri)
            .FirstOrDefault();
    }

    private static MediaAsset CreateAsset(Uri downloadUri)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(downloadUri.AbsoluteUri))).ToLowerInvariant()[..16];
        var fileName = Path.GetFileName(downloadUri.AbsolutePath);
        if (!fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) fileName = $"missions-{hash}.mp4";
        return new MediaAsset($"asset-{hash}", downloadUri, fileName, null, null);
    }

    private static bool TryParseMissionDate(string title, int expectedYear, out DateOnly date)
    {
        var match = DatePattern.Match(Normalize(title));
        if (match.Success && Months.TryGetValue(match.Groups["month"].Value.ToUpperInvariant(), out var month))
        {
            var year = match.Groups["year"].Success ? int.Parse(match.Groups["year"].Value) : expectedYear;
            date = new DateOnly(year, month, int.Parse(match.Groups["day"].Value));
            return true;
        }

        var numeric = NumericDatePattern.Match(title);
        if (numeric.Success)
        {
            var year = 2000 + int.Parse(numeric.Groups["year"].Value);
            date = new DateOnly(year, int.Parse(numeric.Groups["month"].Value), int.Parse(numeric.Groups["day"].Value));
            return true;
        }

        date = default;
        return false;
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

    private static long GetResolution(string text)
    {
        var match = ResolutionPattern.Match(text);
        return match.Success
            ? long.Parse(match.Groups["width"].Value) * long.Parse(match.Groups["height"].Value)
            : 0;
    }

    private static string ExtractMissionTitle(string context, DateOnly date)
    {
        var dateText = date.ToString("dd/MM/yy");
        var dateIndex = context.IndexOf(dateText, StringComparison.Ordinal);
        if (dateIndex < 0) dateIndex = DatePattern.Match(context).Index;
        if (dateIndex <= 0) return $"Informativo Mundial das Missões - {date:dd/MM/yyyy}";

        var title = context[..dateIndex].Trim(' ', '-', '–', '—', ':');
        title = LeadingItemNumberPattern.Replace(title, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(title) ? $"Informativo Mundial das Missões - {date:dd/MM/yyyy}" : title;
    }

    private sealed record Anchor(Uri Uri, string Text);
    private sealed record MissionPost(Uri Uri, DateOnly Date, string Title);

    private static readonly Regex AnchorPattern = new("<a\\b[^>]*\\bhref\\s*=\\s*[\\\"'](?<href>[^\\\"']+)[\\\"'][^>]*>(?<text>[\\s\\S]*?)</a>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MissionPostParagraphPattern = new("<p\\b[^>]*>(?<content>[\\s\\S]*?<a\\b[^>]*\\bhref\\s*=\\s*[\\\"'](?<href>[^\\\"']*informativo-mundial-das-missoes[^\\\"']*)[\\\"'][^>]*>\\s*Download\\s*</a>[\\s\\S]*?)</p>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.CultureInvariant);
    private static readonly Regex ResolutionPattern = new("(?<width>\\d{3,4})\\s*[x×]\\s*(?<height>\\d{3,4})p", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LeadingItemNumberPattern = new("^\\d+\\s*[.\\-–—]?\\s*", RegexOptions.CultureInvariant);
    private static readonly Regex DatePattern = new("(?<day>\\d{1,2})\\s+(?<month>JANEIRO|FEVEREIRO|MARÇO|MARCO|ABRIL|MAIO|JUNHO|JULHO|AGOSTO|SETEMBRO|OUTUBRO|NOVEMBRO|DEZEMBRO)(?:\\s+(?<year>\\d{4}))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NumericDatePattern = new("(?<day>\\d{1,2})/(?<month>\\d{1,2})/(?<year>\\d{2})", RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, int> Months = new Dictionary<string, int>
    {
        ["JANEIRO"] = 1, ["FEVEREIRO"] = 2, ["MARÇO"] = 3, ["MARCO"] = 3, ["ABRIL"] = 4, ["MAIO"] = 5, ["JUNHO"] = 6,
        ["JULHO"] = 7, ["AGOSTO"] = 8, ["SETEMBRO"] = 9, ["OUTUBRO"] = 10, ["NOVEMBRO"] = 11, ["DEZEMBRO"] = 12
    };
}
