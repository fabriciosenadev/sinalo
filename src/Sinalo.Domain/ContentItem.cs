namespace Sinalo.Domain;

public sealed record ContentItem(
    string Id,
    ContentSource Source,
    string Title,
    DateOnly ScheduledDate,
    Uri PageUri,
    IReadOnlyList<MediaAsset> Assets,
    SyncState SyncState = SyncState.Pending,
    bool IsPinned = false,
    string? LocalPath = null)
{
    public Quarter Quarter => Quarter.From(ScheduledDate);

    public bool IsReadyOffline => SyncState == SyncState.Ready;
}
