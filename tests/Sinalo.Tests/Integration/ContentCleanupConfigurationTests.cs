using System.IO;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class ContentCleanupConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Configuration_ShouldPersistCleanupPolicyAndLastRunDate()
    {
        var paths = new Paths(_root);
        await new SinaloDatabase(paths).InitializeAsync();
        var service = (IContentCleanupConfigurationService)new SqliteConfigurationService(paths);

        Assert.False((await service.LoadAsync()).IsEnabled);
        await service.SaveAsync(new ContentCleanupConfiguration(true, 6, 45, new DateOnly(2026, 9, 16)));

        var saved = await service.LoadAsync();
        Assert.True(saved.IsEnabled);
        Assert.Equal(6, saved.NormalizedRetentionMonths);
        Assert.Equal(45, saved.NormalizedGracePeriodDays);
        Assert.Equal(new DateOnly(2026, 9, 16), saved.LastRunDate);
    }

    [Fact]
    public async Task Configuration_ShouldPersistCleanupPolicyWithoutLastRunDate()
    {
        var paths = new Paths(_root);
        await new SinaloDatabase(paths).InitializeAsync();
        var service = (IContentCleanupConfigurationService)new SqliteConfigurationService(paths);

        await service.SaveAsync(new ContentCleanupConfiguration(true, 2, 7));

        var saved = await service.LoadAsync();
        Assert.True(saved.IsEnabled);
        Assert.Equal(2, saved.NormalizedRetentionMonths);
        Assert.Equal(7, saved.NormalizedGracePeriodDays);
        Assert.Null(saved.LastRunDate);
    }

    [Fact]
    public async Task PlaybackConfiguration_ShouldPersistAnUnconfiguredOutput()
    {
        var paths = new Paths(_root);
        await new SinaloDatabase(paths).InitializeAsync();
        var service = new SqliteConfigurationService(paths);

        await service.SaveAsync(new PlaybackConfiguration(null, null));

        var saved = await service.LoadAsync();
        Assert.Null(saved.FullscreenScreenNumber);
        Assert.Null(saved.FullscreenMonitorKey);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class Paths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp"), Path.Combine(root, "data", "sinalo.db"));
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
