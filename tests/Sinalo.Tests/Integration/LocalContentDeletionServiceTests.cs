using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class LocalContentDeletionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheLocalVideoAndCatalogEntry()
    {
        var paths = new TestPaths(_root); paths.EnsureFolders();
        var file = Path.Combine(paths.GetPaths().ContentPath, "video.mp4");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        var item = Item(file);
        var catalog = new Catalog(item);

        await new LocalContentDeletionService(catalog, paths).DeleteAsync(item.Id);

        Assert.False(File.Exists(file));
        Assert.Equal(item.Id, catalog.DeletedId);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectPathsOutsideTheContentFolder()
    {
        var paths = new TestPaths(_root); paths.EnsureFolders();
        var item = Item(Path.Combine(_root, "outside.mp4"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalContentDeletionService(new Catalog(item), paths).DeleteAsync(item.Id));
    }

    private static ContentItem Item(string path) => new("video", ContentSource.Missions, "Vídeo", new DateOnly(2026, 8, 8), new Uri("https://example.test/video"), [], SyncState.Ready, LocalPath: path);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class Catalog(ContentItem item) : IContentCatalog
    {
        public string? DeletedId { get; private set; }
        public Task<ContentItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ContentItem?>(id == item.Id ? item : null);
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) { DeletedId = id; return Task.CompletedTask; }
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>([]);
    }

    private sealed class TestPaths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp", "downloads"), Path.Combine(root, "data", "sinalo.db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders() { Directory.CreateDirectory(_paths.ContentPath); Directory.CreateDirectory(_paths.TempDownloadsPath); }
    }
}
