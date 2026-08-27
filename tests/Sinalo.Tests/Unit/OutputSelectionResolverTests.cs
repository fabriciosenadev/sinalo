using Sinalo.Application.Monitors;
using Sinalo.Application.Playback;

namespace Sinalo.Tests.Unit;

public sealed class OutputSelectionResolverTests
{
    private static readonly OutputProfile Primary = new(@"\\.\DISPLAY1", "Tela 1 · Principal", 1, 0, 0, 1920, 1080, true);
    private static readonly OutputProfile Secondary = new(@"\\.\DISPLAY2", "Tela 2", 2, 1920, 0, 1920, 1080, false);

    [Fact]
    public void Resolve_PrefersTheStableMonitorKey()
    {
        var output = OutputSelectionResolver.Resolve(new PlaybackConfiguration(1, Secondary.MonitorKey), [Primary, Secondary]);

        Assert.Equal(Secondary, output);
    }

    [Fact]
    public void Resolve_UsesLegacyScreenNumberWhenNoMonitorKeyWasStored()
    {
        var output = OutputSelectionResolver.Resolve(new PlaybackConfiguration(2), [Primary, Secondary]);

        Assert.Equal(Secondary, output);
    }

    [Fact]
    public void Resolve_DoesNotRedirectAnUnavailableConfiguredMonitor()
    {
        var output = OutputSelectionResolver.Resolve(new PlaybackConfiguration(2, Secondary.MonitorKey), [Primary]);

        Assert.Null(output);
    }

    [Fact]
    public void Resolve_FallsBackToTheWindowsPrimaryOutput()
    {
        var output = OutputSelectionResolver.Resolve(new PlaybackConfiguration(null), [Secondary, Primary]);

        Assert.Equal(Primary, output);
    }

    [Fact]
    public void Resolve_ReturnsNullWhenWindowsHasNoOutput()
    {
        Assert.Null(OutputSelectionResolver.Resolve(new PlaybackConfiguration(null), []));
    }
}
