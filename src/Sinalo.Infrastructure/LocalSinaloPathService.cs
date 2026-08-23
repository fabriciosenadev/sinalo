using Sinalo.Application.Storage;

namespace Sinalo.Infrastructure;

public sealed class LocalSinaloPathService(string? contentPath = null, string? rootPath = null) : ISinaloPathService, IContentPathConfigurationService
{
    private const string ApplicationName = "Sinalo";
    private const string ContentPathFileName = "content-path.txt";
    private string? _contentPath = contentPath;
    private readonly string _rootPath = rootPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationName);

    public SinaloPaths GetPaths()
    {
        var dataPath = Path.Combine(_rootPath, "data");

        return new SinaloPaths(
            _rootPath,
            dataPath,
            ResolveContentPath(dataPath),
            Path.Combine(_rootPath, "cache"),
            Path.Combine(_rootPath, "logs"),
            Path.Combine(_rootPath, "temp", "downloads"),
            Path.Combine(dataPath, "sinalo.db"));
    }

    public string GetContentPath() => GetPaths().ContentPath;

    public void SaveContentPath(string contentPath)
    {
        if (string.IsNullOrWhiteSpace(contentPath)) throw new ArgumentException("Informe uma pasta para o conteúdo local.", nameof(contentPath));

        var fullPath = Path.GetFullPath(contentPath.Trim());
        Directory.CreateDirectory(fullPath);
        var probePath = Path.Combine(fullPath, $".sinalo-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, "Sinalo");
        }
        finally
        {
            if (File.Exists(probePath)) File.Delete(probePath);
        }

        var dataPath = Path.Combine(_rootPath, "data");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(dataPath, ContentPathFileName), fullPath);
        _contentPath = fullPath;
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

    private string ResolveContentPath(string dataPath)
    {
        if (!string.IsNullOrWhiteSpace(_contentPath)) return _contentPath;

        var configurationPath = Path.Combine(dataPath, ContentPathFileName);
        if (File.Exists(configurationPath))
        {
            var configuredPath = File.ReadAllText(configurationPath).Trim();
            if (!string.IsNullOrWhiteSpace(configuredPath)) return configuredPath;
        }

        return Path.Combine(_rootPath, "content");
    }
}
