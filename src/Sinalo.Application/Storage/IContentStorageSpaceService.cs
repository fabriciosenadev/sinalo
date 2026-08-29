using Sinalo.Domain;

namespace Sinalo.Application.Storage;

public sealed record ContentStorageSpaceAssessment(
    string VolumeName,
    long AvailableBytes,
    long KnownDownloadBytes,
    long RequiredBytes,
    int UnknownItemCount)
{
    public bool HasUnknownSizes => UnknownItemCount > 0;
    public bool HasSufficientSpace => AvailableBytes >= RequiredBytes;
}

public sealed class InsufficientStorageSpaceException(ContentStorageSpaceAssessment assessment)
    : InvalidOperationException($"Espaço insuficiente em {assessment.VolumeName}. São necessários aproximadamente {Format(assessment.RequiredBytes)}; há {Format(assessment.AvailableBytes)} disponível.")
{
    public ContentStorageSpaceAssessment Assessment { get; } = assessment;

    private static string Format(long bytes) => $"{bytes / 1024d / 1024d / 1024d:0.0} GB";
}

public sealed class StorageSpaceCriticalException()
    : IOException("O download foi interrompido porque o espaço em disco ficou crítico.");

public interface IContentStorageSpaceService
{
    Task<ContentStorageSpaceAssessment> AssessAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default);

    Task<bool> HasMinimumFreeSpaceAsync(string path, long minimumFreeBytes, CancellationToken cancellationToken = default);
}
