namespace Sinalo.Application.Storage;

public sealed record SinaloPaths(
    string RootPath,
    string DataPath,
    string ContentPath,
    string CachePath,
    string LogsPath,
    string TempDownloadsPath,
    string DatabasePath);
