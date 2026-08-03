namespace Sinalo.Domain;

public sealed record MediaAsset(
    string Id,
    Uri DownloadUri,
    string FileName,
    long? ExpectedSizeBytes,
    string? Sha256);
