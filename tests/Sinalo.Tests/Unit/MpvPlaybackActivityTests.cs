using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class MpvPlaybackActivityTests
{
    [Theory]
    [InlineData("{\"event\":\"file-loaded\"}", true)]
    [InlineData("{\"event\":\"end-file\"}", false)]
    [InlineData("{\"event\":\"idle\"}", false)]
    public void ParsePlaybackActivityEvent_RecognizesMediaLifecycle(string json, bool expected) =>
        Assert.Equal(expected, MpvPlaybackLauncher.ParsePlaybackActivityEvent(json));

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"event\":5}")]
    [InlineData("{\"event\":\"property-change\"}")]
    public void ParsePlaybackActivityEvent_IgnoresOtherResponses(string json) =>
        Assert.Null(MpvPlaybackLauncher.ParsePlaybackActivityEvent(json));
}
