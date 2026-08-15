namespace Sinalo.Domain;

/// <summary>
/// Defines which dates from the operational Saturday window should be downloaded.
/// An empty selection means that the complete quarter is requested instead.
/// </summary>
public sealed record DownloadSelection(bool PreviousSaturday, bool CurrentSaturday, bool NextSaturday)
{
    public static DownloadSelection Quarterly { get; } = new(false, false, false);

    public static DownloadSelection SaturdayWindow { get; } = new(true, true, true);

    public bool DownloadsQuarterly => !PreviousSaturday && !CurrentSaturday && !NextSaturday;

    public static DownloadSelection FromLegacyPolicy(AvailabilityPolicy policy) =>
        policy == AvailabilityPolicy.RollingSaturday ? SaturdayWindow : Quarterly;
}
