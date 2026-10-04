using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class LinkedVideoCandidateSelectionTests
{
    [Theory]
    [InlineData(ContentSource.Health, "health")]
    [InlineData(ContentSource.Missions, "missions")]
    [InlineData(ContentSource.ProvaiEVede, "provai-e-vede")]
    public void AutomaticSync_DoesNotSendManualLinkToOfficialDownloader(ContentSource source, string prefix)
    {
        var date = new DateOnly(2026, 8, 8);
        var asset = new MediaAsset("asset", new Uri("https://example.test/video.mp4"), "video.mp4", 1024, null);
        var manual = new ContentItem($"{prefix}-youtube-RN92XFsaPHE", source, "Manual", date, new Uri("https://youtu.be/RN92XFsaPHE"), [asset]);
        var official = new ContentItem($"{prefix}-2026-08-08", source, "Oficial", date, new Uri("https://example.test/video"), [asset]);

        var result = SynchronizationCandidateSelector.Select(source, [manual, official], DownloadSelection.Quarterly, new SaturdayWindowService(), date);

        Assert.Equal(official.Id, Assert.Single(result).Id);
    }
}
