using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class DomainModelTests
{
    [Fact]
    public void Quarter_ShouldCalculateAndFormatItsValue()
    {
        var quarter = Quarter.From(new DateOnly(2026, 8, 8));

        Assert.Equal(2026, quarter.Year);
        Assert.Equal(3, quarter.Number);
        Assert.Equal("2026-T3", quarter.ToString());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 2)]
    [InlineData(7, 3)]
    [InlineData(12, 4)]
    public void Quarter_ShouldMapEveryMonthToItsQuarter(int month, int expectedQuarter)
    {
        var quarter = Quarter.From(new DateOnly(2026, month, 1));

        Assert.Equal(expectedQuarter, quarter.Number);
    }

    [Fact]
    public void ContentItem_ShouldExposeQuarterAndOfflineReadiness()
    {
        var asset = new MediaAsset(
            "asset-1",
            new Uri("https://example.test/video.mp4"),
            "video.mp4",
            1024,
            "abc");
        var item = new ContentItem(
            "missions-2026-08-08",
            ContentSource.Missions,
            "Informativo",
            new DateOnly(2026, 8, 8),
            new Uri("https://example.test/page"),
            [asset],
            SyncState.Ready,
            true);

        Assert.Equal("2026-T3", item.Quarter.ToString());
        Assert.True(item.IsReadyOffline);
        Assert.True(item.IsPinned);
        Assert.Single(item.Assets);
        Assert.Equal("video.mp4", item.Assets[0].FileName);
    }

    [Fact]
    public void ContentItem_ShouldNotBeOfflineBeforeItIsReady()
    {
        var item = new ContentItem(
            "health-2026-08-08",
            ContentSource.Health,
            "Minuto de Saúde",
            new DateOnly(2026, 8, 8),
            new Uri("https://example.test/page"),
            []);

        Assert.Equal(SyncState.Pending, item.SyncState);
        Assert.False(item.IsReadyOffline);
    }
}
