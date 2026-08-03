namespace Sinalo.Domain;

public enum SyncState
{
    Pending,
    Downloading,
    Validating,
    Ready,
    Failed,
    OnlineOnly
}
