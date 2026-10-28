using System.IO;
using System.Text.Json;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;
using static Sinalo.Tests.HealthYouTubeTestData;

namespace Sinalo.Tests.Unit;
public sealed class ChannelVideoServiceTests
{
    [Theory]
    [InlineData("https://youtube.com/@VidaeSaudeUCB", "https://www.youtube.com/@VidaeSaudeUCB/videos")]
    [InlineData("https://www.youtube.com/@VidaeSaudeUCB/videos/", "https://www.youtube.com/@VidaeSaudeUCB/videos")]
    [InlineData("https://www.youtube.com/channel/UCufCWU_e8Q_ONyBgf90fZdA", "https://www.youtube.com/channel/UCufCWU_e8Q_ONyBgf90fZdA/videos")]
    public void ChannelUrl_NormalizesOnlyChannelAddresses(string input, string expected) => Assert.Equal(expected, HealthYouTubeConfiguration.NormalizeChannelUrl(input).AbsoluteUri);
    [Theory]
    [InlineData("http://www.youtube.com/@name")]
    [InlineData("https://www.youtube.com/watch?v=123")]
    [InlineData("https://www.youtube.com/playlist?list=PL123")]
    [InlineData("https://www.youtube.com/@name/shorts")]
    [InlineData("https://www.youtube.com.evil.test/@name")]
    [InlineData("https://user@www.youtube.com/@name")]
    [InlineData("https://www.youtube.com:123/@name")]
    [InlineData("https://www.youtube.com/@name#other")]
    public void ChannelUrl_RejectsAmbiguousAndUnsafeAddresses(string input) => Assert.Throws<ArgumentException>(() => HealthYouTubeConfiguration.NormalizeChannelUrl(input));
    [Fact]
    public void Configuration_RequiresVerificationAndSupportedQuality()
    {
        new HealthYouTubeConfiguration().Validate();
        Assert.Throws<ArgumentException>(() => (new HealthYouTubeConfiguration(Enabled: true)).Validate());
        Assert.Throws<ArgumentException>(() => (Configuration with { MaximumHeight = 2160 }).Validate());
        Assert.Throws<ArgumentException>(() => (Configuration with { ChannelId = "UCaaaaaaaaaaaaaaaaaaaaaa" }).Validate());
    }
    [Theory]
    [InlineData("is_upcoming")][InlineData("is_live")][InlineData("post_live")][InlineData("")]
    public void ParseVideo_ExcludesScheduledAndUnprocessedVideos(string state) => Assert.Null(ChannelVideoService.ParseVideo(Metadata(135, state: state), Configuration, Now));
    [Theory]
    [InlineData("private")][InlineData("needs_auth")][InlineData("premium_only")][InlineData("unlisted")]
    public void ParseVideo_RequiresPublicAvailability(string availability) => Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, availability: availability), Configuration, Now));
    [Fact]
    public void ParseVideo_RequiresDateChannelAndCompatibleFormat()
    {
        Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, date: null), Configuration, Now));
        Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, date: "20261201"), Configuration, Now));
        Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, timestamp: Now.AddDays(1).ToUnixTimeSeconds()), Configuration, Now));
        Assert.Throws<InvalidDataException>(() => ChannelVideoService.ParseVideo(Metadata(1, channel: "other"), Configuration, Now));
        Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, codec: "av01"), Configuration, Now));
        Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, height: 1080), Configuration, Now));
    }
    [Fact]
    public void ParseVideo_DoesNotTurnPublicationIntoASaturdayAndEpisodeIsOptional()
    {
        var video = ChannelVideoService.ParseVideo(Metadata(130), Configuration, Now)!;
        Assert.Equal(new DateOnly(2026, 10, 21), video.Video.PublishedDate);
        Assert.Equal(DayOfWeek.Wednesday, video.Video.PublishedDate.DayOfWeek);
        Assert.Equal(130, video.Episode);
        Assert.Null(ChannelVideoService.ParseVideo(Metadata(1, title: "Saúde sem número"), Configuration, Now)!.Episode);
        Assert.NotNull(ChannelVideoService.ParseVideo(Metadata(1, state: "was_live"), Configuration, Now));
        var timestamp = Now.AddDays(-1);
        Assert.Equal(timestamp, ChannelVideoService.ParseVideo(Metadata(1, timestamp: timestamp.ToUnixTimeSeconds()), Configuration, Now)!.PublishedAt);
    }
    [Fact]
    public void Selection_UsesPublicationNotHighestEpisodeAndNeverReturnsMoreThanFour()
    {
        var list = ChannelVideoSelection.Recent([Video(130, "20261021"), Video(5, "20261027"), Video(128, "20261007"), Video(129, "20261014"), Video(127, "20260930"), Video(5, "20261027")]);
        Assert.Equal([5, 130, 129, 128], list.Select(x => x.Episode!.Value));
    }
    [Fact]
    public async Task Query_SkipsInterleavedScheduledVideosAndUsesBoundedReadOnlyCommands()
    {
        var inspections = 0;
        var service = new ChannelVideoService(runner: (args, _, _) =>
        {
            Assert.Contains("--skip-download", args);
            if (args.Contains("--flat-playlist"))
            {
                Assert.Contains("100", args);
                var entries = new[] { 131, 134, 135, 133, 132, 130, 129, 128, 127 }.Select(n => new { id = Id(n), live_status = n >= 132 ? "is_upcoming" : (string?)null });
                return Result(JsonSerializer.Serialize(new { channel_id = ChannelId, channel = "Vida e Saúde UCB", entries }));
            }
            inspections++;
            var id = args[^1].Split('=')[1]; var number = int.Parse(id[5..]);
            return Result(Metadata(number, date: new DateOnly(2026, 10, 28).AddDays(-(131 - number) * 7).ToString("yyyyMMdd")));
        }, clock: () => Now);
        var result = await service.ListRecentAsync(Configuration);
        Assert.Equal([131, 130, 129, 128], result.Videos.Select(x => x.Episode!.Value));
        Assert.Equal(4, result.Excluded); Assert.False(result.IsLimited); Assert.Equal(5, inspections);
        await service.ListRecentAsync(Configuration); Assert.Equal(5, inspections);
    }
    [Fact]
    public async Task Query_DoesNotIgnoreChangedChannelOrFailedMetadata()
    {
        var service = new ChannelVideoService(runner: (_, _, _) => Result("""{"channel_id":"UCaaaaaaaaaaaaaaaaaaaaaa","channel":"Other","entries":[]}"""));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ListRecentAsync(Configuration));
        var failed = new ChannelVideoService(runner: (_, _, _) => Task.FromResult(new VideoToolResult(1, "", "Network")));
        await Assert.ThrowsAsync<IOException>(() => failed.VerifyAsync(Configuration.ChannelUrl));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListRecentAsync(Configuration with { Enabled = false }));
    }
    [Fact]
    public async Task Verify_ReturnsCanonicalIdentityAndRejectsMalformedResult()
    {
        var service = new ChannelVideoService(runner: (_, _, _) => Result(JsonSerializer.Serialize(new { channel_id = ChannelId, channel = "Vida e Saúde UCB" })));
        Assert.Equal(ChannelId, (await service.VerifyAsync("https://www.youtube.com/@VidaeSaudeUCB")).Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ChannelVideoService(runner: (_, _, _) => Result("{}")).VerifyAsync(Configuration.ChannelUrl));
    }
    private static Task<VideoToolResult> Result(string json) => Task.FromResult(new VideoToolResult(0, json, ""));
    [Fact]
    public async Task Query_PartialMetadataFailureKeepsOnlyVerifiedResults()
    {
        var inspections = 0;
        var service = new ChannelVideoService(runner: (args, _, _) =>
        {
            if (args.Contains("--flat-playlist")) return Result(JsonSerializer.Serialize(new { channel_id = ChannelId, channel = "Health", entries = new[] { new { id = Id(1) }, new { id = Id(2) } } }));
            return ++inspections == 1 ? Result(Metadata(1)) : throw new IOException("Offline");
        }, clock: () => Now);
        var result = await service.ListRecentAsync(Configuration);
        Assert.Single(result.Videos); Assert.True(result.IsLimited); Assert.NotEmpty(result.Warnings);
    }
    [Fact]
    public async Task Query_DetectsDateDisorderAndSortsInsteadOfTakingHighestEpisode()
    {
        var service = new ChannelVideoService(runner: (args, _, _) =>
        {
            if (args.Contains("--flat-playlist")) return Result(JsonSerializer.Serialize(new { channel_id = ChannelId, channel = "Health", entries = new[] { 5, 130, 131, 128, 127, 6 }.Select(n => new { id = Id(n) }) }));
            var n = int.Parse(args[^1].Split('=')[1][5..]);
            return Result(Metadata(n, date: n == 6 ? "20261028" : n == 131 ? "20261027" : "20261021"));
        }, clock: () => Now);
        var result = await service.ListRecentAsync(Configuration);
        Assert.True(result.IsLimited); Assert.NotEmpty(result.Warnings);
        Assert.Equal([6, 131, 130, 128], result.Videos.Select(x => x.Episode!.Value));
    }
    [Fact]
    public void CompatibleFormats_RejectProtectedOrIncompatibleAudioAndEstimateBothStreams()
    {
        const string json = """
        {"id":"RN92XFsaPHE","formats":[
          {"format_id":"140","ext":"m4a","acodec":"mp4a.40.2","abr":128,"filesize":1000},
          {"format_id":"999","ext":"m4a","acodec":"other","abr":999,"filesize":2000},
          {"format_id":"137","ext":"mp4","vcodec":"avc1","acodec":"none","height":720,"filesize":4000},
          {"format_id":"138","ext":"mp4","vcodec":"avc1","acodec":"mp4a","height":1080,"has_drm":true} ]}
        """;
        var format = Assert.Single(LinkedVideoService.ParseMetadata(json, new("https://youtu.be/RN92XFsaPHE"), true).Formats);
        Assert.Equal("137+140", format.Selector); Assert.Equal(5000, format.EstimatedBytes);
    }
}
