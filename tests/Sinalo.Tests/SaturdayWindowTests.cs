using Sinalo.Domain;

namespace Sinalo.Tests;

public sealed class SaturdayWindowTests
{
    [Theory]
    [InlineData("2026-08-01", "2026-07-25", "2026-08-01", "2026-08-08")]
    [InlineData("2026-08-02", "2026-08-01", "2026-08-08", "2026-08-15")]
    [InlineData("2026-08-05", "2026-08-01", "2026-08-08", "2026-08-15")]
    public void From_ShouldReturnPreviousCurrentAndNextOperationalSaturdays(
        string referenceDate,
        string previous,
        string current,
        string next)
    {
        var window = SaturdayWindow.From(DateOnly.Parse(referenceDate));

        Assert.Equal(DateOnly.Parse(previous), window.Previous);
        Assert.Equal(DateOnly.Parse(current), window.Current);
        Assert.Equal(DateOnly.Parse(next), window.Next);
        Assert.Equal([window.Previous, window.Current, window.Next], window.InPriorityOrder);
    }
}
