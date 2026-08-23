using System.IO;
using Sinalo.Application.Catalog;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class LocalContentPathMigrationServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MoveAsync_ShouldTransferVideosUpdateTheCatalogAndPersistTheNewFolder()
    {
        var paths = new LocalSinaloPathService(rootPath: _rootPath);
        var previousPath = paths.GetContentPath();
        var sourceFile = Path.Combine(previousPath, "2026-T3", "missions", "video.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        await File.WriteAllBytesAsync(sourceFile, [1, 2, 3]);
        var catalog = new Catalog(Item(sourceFile));
        var targetPath = Path.Combine(_rootPath, "conteudo-novo");

        await new LocalContentPathMigrationService(paths, catalog).MoveAsync(targetPath);

        var targetFile = Path.Combine(targetPath, "2026-T3", "missions", "video.mp4");
        Assert.False(File.Exists(sourceFile));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(targetFile));
        Assert.Equal(targetFile, catalog.Item.LocalPath);
        Assert.Equal(targetPath, new LocalSinaloPathService(rootPath: _rootPath).GetContentPath());
    }

    [Fact]
    public async Task MoveAsync_ShouldKeepTheOldFolderWhenAConflictingFileExists()
    {
        var paths = new LocalSinaloPathService(rootPath: _rootPath);
        var sourceFile = Path.Combine(paths.GetContentPath(), "video.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        await File.WriteAllBytesAsync(sourceFile, [1, 2, 3]);
        var catalog = new Catalog(Item(sourceFile));
        var targetPath = Path.Combine(_rootPath, "conteudo-novo");
        Directory.CreateDirectory(targetPath);
        await File.WriteAllBytesAsync(Path.Combine(targetPath, "video.mp4"), [4]);

        await Assert.ThrowsAsync<IOException>(() => new LocalContentPathMigrationService(paths, catalog).MoveAsync(targetPath));

        Assert.True(File.Exists(sourceFile));
        Assert.Equal(paths.GetPaths().ContentPath, paths.GetContentPath());
    }

    private static ContentItem Item(string localPath) => new(
        "item",
        ContentSource.Missions,
        "Vídeo",
        new DateOnly(2026, 8, 22),
        new Uri("https://example.test/video"),
        [],
        SyncState.Ready,
        LocalPath: localPath);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, recursive: true);
    }

    private sealed class Catalog(ContentItem item) : IContentCatalog
    {
        public ContentItem Item { get; private set; } = item;

        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>([]);
        public Task RelocateLocalPathsAsync(string previousContentPath, string newContentPath, CancellationToken cancellationToken = default)
        {
            var suffix = Path.GetRelativePath(previousContentPath, Item.LocalPath!);
            Item = Item with { LocalPath = Path.Combine(newContentPath, suffix) };
            return Task.CompletedTask;
        }
    }
}
