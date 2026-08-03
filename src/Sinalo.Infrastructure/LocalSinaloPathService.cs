using Sinalo.Application.Storage;

namespace Sinalo.Infrastructure;

public sealed class LocalSinaloPathService(string? contentPath = null) : ISinaloPathService
{
    private const string ApplicationName = "Sinalo";
    private readonly string? _contentPath = contentPath;

    public SinaloPaths GetPaths()
    {
        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationName);
        var dataPath = Path.Combine(rootPath, "data");

        return new SinaloPaths(
            rootPath,
            dataPath,
            _contentPath ?? Path.Combine(rootPath, "content"),
            Path.Combine(rootPath, "cache"),
            Path.Combine(rootPath, "logs"),
            Path.Combine(rootPath, "temp", "downloads"),
            Path.Combine(dataPath, "sinalo.db"));
    }

    public void EnsureFolders()
    {
        var paths = GetPaths();
        Directory.CreateDirectory(paths.RootPath);
        Directory.CreateDirectory(paths.DataPath);
        Directory.CreateDirectory(paths.ContentPath);
        Directory.CreateDirectory(paths.CachePath);
        Directory.CreateDirectory(paths.LogsPath);
        Directory.CreateDirectory(paths.TempDownloadsPath);
    }
}
