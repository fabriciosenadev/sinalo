using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class LocalContentCleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CleanIfDueAsync_ShouldRemoveExpiredUnpinnedVideoAndKeepPinnedVideo()
    {
        var paths = new Paths(_root); paths.EnsureFolders();
        var old = CreateFile(paths, "old.mp4", new DateOnly(2026, 1, 3));
        var pinned = CreateFile(paths, "pinned.mp4", new DateOnly(2026, 1, 10), true);
        var catalog = new Catalog([old, pinned]);
        var configuration = new Configuration(new ContentCleanupConfiguration(true, 3, 30));

        var result = await new LocalContentCleanupService(catalog, paths, configuration).CleanIfDueAsync(new DateOnly(2026, 9, 15));

        Assert.True(result.WasRun);
        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(1, result.SkippedPinnedCount);
        Assert.False(File.Exists(old.LocalPath));
        Assert.True(File.Exists(pinned.LocalPath));
        Assert.Equal([old.Id], catalog.Deleted);
        Assert.Equal(new DateOnly(2026, 9, 15), configuration.Value.LastRunDate);
    }

    [Fact]
    public async Task CleanIfDueAsync_ShouldNotRunTwiceInTheSameMonth()
    {
        var paths = new Paths(_root); paths.EnsureFolders();
        var old = CreateFile(paths, "old.mp4", new DateOnly(2026, 1, 3));
        var catalog = new Catalog([old]);
        var configuration = new Configuration(new ContentCleanupConfiguration(true, 3, 30, new DateOnly(2026, 9, 1)));

        var result = await new LocalContentCleanupService(catalog, paths, configuration).CleanIfDueAsync(new DateOnly(2026, 9, 15));

        Assert.False(result.WasRun);
        Assert.True(File.Exists(old.LocalPath));
        Assert.Empty(catalog.Deleted);
    }

    [Fact]
    public async Task CleanIfDueAsync_ShouldNeverDeleteFileOutsideTheConfiguredContentPath()
    {
        var paths = new Paths(_root); paths.EnsureFolders();
        var outside = Path.Combine(_root, "outside.mp4");
        await File.WriteAllBytesAsync(outside, [1, 2, 3]);
        var item = new ContentItem("outside", ContentSource.Health, "Fora", new DateOnly(2026, 1, 3), new Uri("https://example.test"), [], SyncState.Ready, LocalPath: outside);
        var catalog = new Catalog([item]);

        var result = await new LocalContentCleanupService(catalog, paths, new Configuration(new ContentCleanupConfiguration(true))).CleanIfDueAsync(new DateOnly(2026, 9, 15));

        Assert.Equal(0, result.RemovedCount);
        Assert.True(File.Exists(outside));
        Assert.Empty(catalog.Deleted);
    }

    private ContentItem CreateFile(Paths paths, string name, DateOnly date, bool pinned = false)
    {
        var path = Path.Combine(paths.GetPaths().ContentPath, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return new(name, ContentSource.Missions, name, date, new Uri("https://example.test/" + name), [], SyncState.Ready, pinned, path);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class Configuration(ContentCleanupConfiguration value) : IContentCleanupConfigurationService
    {
        public ContentCleanupConfiguration Value { get; private set; } = value;
        public Task<ContentCleanupConfiguration> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(ContentCleanupConfiguration configuration, CancellationToken cancellationToken = default) { Value = configuration; return Task.CompletedTask; }
    }

    private sealed class Catalog(IReadOnlyList<ContentItem> items) : IContentCatalog
    {
        public List<string> Deleted { get; } = [];
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>(items.Where(item => item.Source == source && !Deleted.Contains(item.Id)).ToArray());
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) { Deleted.Add(id); return Task.CompletedTask; }
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Paths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp"), Path.Combine(root, "data", "sinalo.db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders() => Directory.CreateDirectory(_paths.ContentPath);
    }
}
