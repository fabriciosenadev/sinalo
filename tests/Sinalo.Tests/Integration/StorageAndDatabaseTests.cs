using System.IO;
using Microsoft.Data.Sqlite;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class StorageAndDatabaseTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LocalPathService_ShouldCreateEveryRequiredFolder()
    {
        var service = new LocalSinaloPathService(Path.Combine(_rootPath, "custom-content"));

        service.EnsureFolders();
        var paths = service.GetPaths();

        Assert.True(Directory.Exists(paths.DataPath));
        Assert.True(Directory.Exists(paths.ContentPath));
        Assert.True(Directory.Exists(paths.CachePath));
        Assert.True(Directory.Exists(paths.LogsPath));
        Assert.True(Directory.Exists(paths.TempDownloadsPath));
        Assert.Equal(Path.Combine(_rootPath, "custom-content"), paths.ContentPath);
    }

    [Fact]
    public async Task SinaloDatabase_ShouldCreateTheLocalDatabase()
    {
        var pathService = new TestPathService(_rootPath);
        var database = new SinaloDatabase(pathService);

        await database.InitializeAsync();

        Assert.True(File.Exists(pathService.GetPaths().DatabasePath));
        Assert.True(Directory.Exists(pathService.GetPaths().ContentPath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class TestPathService(string rootPath) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(
            rootPath,
            Path.Combine(rootPath, "data"),
            Path.Combine(rootPath, "content"),
            Path.Combine(rootPath, "cache"),
            Path.Combine(rootPath, "logs"),
            Path.Combine(rootPath, "temp", "downloads"),
            Path.Combine(rootPath, "data", "sinalo.db"));

        public SinaloPaths GetPaths() => _paths;

        public void EnsureFolders()
        {
            Directory.CreateDirectory(_paths.DataPath);
            Directory.CreateDirectory(_paths.ContentPath);
            Directory.CreateDirectory(_paths.CachePath);
            Directory.CreateDirectory(_paths.LogsPath);
            Directory.CreateDirectory(_paths.TempDownloadsPath);
        }
    }
}
