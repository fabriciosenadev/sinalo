namespace Sinalo.Application.Storage;

public sealed record ContentCleanupConfiguration(
    bool IsEnabled = false,
    int RetentionMonths = 3,
    int GracePeriodDays = 30,
    DateOnly? LastRunDate = null)
{
    public int NormalizedRetentionMonths => Math.Clamp(RetentionMonths, 1, 24);
    public int NormalizedGracePeriodDays => Math.Clamp(GracePeriodDays, 0, 180);
}

public sealed record ContentCleanupResult(int RemovedCount, long ReclaimedBytes, int SkippedPinnedCount, bool WasRun)
{
    public static ContentCleanupResult NotDue => new(0, 0, 0, false);
}

public interface IContentCleanupConfigurationService
{
    Task<ContentCleanupConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ContentCleanupConfiguration configuration, CancellationToken cancellationToken = default);
}

public interface IContentCleanupService
{
    Task<ContentCleanupResult> CleanIfDueAsync(DateOnly today, CancellationToken cancellationToken = default);
}
