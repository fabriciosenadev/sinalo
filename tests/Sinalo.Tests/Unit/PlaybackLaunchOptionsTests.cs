using Sinalo.Application.Monitors;
using Sinalo.Application.Playback;

namespace Sinalo.Tests.Unit;

public sealed class PlaybackLaunchOptionsTests
{
    [Fact]
    public void OutputProfile_ShouldPreserveTheSelectedMonitorAndConvertThePlayerIndex()
    {
        var output = new OutputProfile(@"\\.\DISPLAY2", "Tela 2", 2, -1920, 0, 1920, 1080, false);

        var options = new PlaybackLaunchOptions(output);

        Assert.Equal(@"\\.\DISPLAY2", options.MonitorKey);
        Assert.Equal("Tela 2", options.OutputLabel);
        Assert.Equal(1, options.PlayerScreenIndex);
        Assert.True(options.HasOutputBounds);
        Assert.Equal(-1920, options.BoundsX);
    }

    [Fact]
    public void LegacyScreenNumber_ShouldRemainUsable()
    {
        var options = new PlaybackLaunchOptions(1);

        Assert.Equal(0, options.PlayerScreenIndex);
        Assert.False(options.HasOutputBounds);
        Assert.Equal("Tela 1", options.OutputLabel);
    }
}
