namespace Sinalo.Application.Updates;

public sealed record AvailableUpdate(Version Version, string Notes, Uri InstallerUri, Uri? ChecksumUri, string? Digest);
public sealed record DownloadedUpdate(AvailableUpdate Update, string InstallerPath);
public sealed record UpdateDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double Percentage => TotalBytes is > 0 ? BytesReceived * 100d / TotalBytes.Value : 0;
}

public interface IApplicationUpdateService
{
    Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task<DownloadedUpdate> DownloadAsync(AvailableUpdate update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}

public interface IUpdateInstallerLauncher
{
    void Launch(string installerPath);
}
