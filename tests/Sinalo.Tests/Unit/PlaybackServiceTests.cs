using Sinalo.Application.Catalog;
using Sinalo.Application.Playback;
using Sinalo.Domain;
using System.IO;

namespace Sinalo.Tests.Unit;

public sealed class PlaybackServiceTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"), "video.mp4");

    [Fact]
    public async Task PlayAsync_ShouldLaunchReadyFileAndRecordItsHistory()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllBytesAsync(_filePath, [1, 2, 3]);
        var catalog = new Catalog(Item(SyncState.Ready, _filePath));
        var result = await new PlaybackService(catalog, new Launcher(true)).PlayAsync("item", new PlaybackLaunchOptions(2));

        Assert.True(result.Started);
        Assert.Equal("VLC", result.Message);
        Assert.Equal(1, result.Item!.PlayCount);
        Assert.NotNull(result.Item.LastPlayedAtUtc);
        Assert.Equal(result.Item.FirstPlayedAtUtc, result.Item.LastPlayedAtUtc);
        Assert.Single(catalog.PlayedIds);
    }

    [Theory]
    [InlineData(SyncState.Pending, true, "pronto")]
    [InlineData(SyncState.Ready, true, "não foi encontrado")]
    public async Task PlayAsync_ShouldRejectItemsThatCannotBePlayed(SyncState state, bool hasPath, string messagePart)
    {
        var path = hasPath ? Path.Combine(Path.GetTempPath(), "Sinalo.Tests", "missing.mp4") : null;
        var catalog = new Catalog(Item(state, path));
        var launcher = new Launcher(true);
        var result = await new PlaybackService(catalog, launcher).PlayAsync("item", new PlaybackLaunchOptions(2));

        Assert.False(result.Started);
        Assert.Contains(messagePart, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(launcher.WasCalled);
        Assert.Empty(catalog.PlayedIds);
    }

    [Fact]
    public async Task PlayAsync_ShouldNotRecordWhenPlayerFailsOrItemIsUnknown()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllBytesAsync(_filePath, [1]);
        var catalog = new Catalog(Item(SyncState.Ready, _filePath));

        var failed = await new PlaybackService(catalog, new Launcher(false)).PlayAsync("item", new PlaybackLaunchOptions(2));
        var missing = await new PlaybackService(catalog, new Launcher(true)).PlayAsync("unknown", new PlaybackLaunchOptions(2));

        Assert.False(failed.Started);
        Assert.False(missing.Started);
        Assert.Empty(catalog.PlayedIds);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static ContentItem Item(SyncState state, string? path) => new("item", ContentSource.ProvaiEVede, "Vídeo", new DateOnly(2026, 8, 8), new Uri("https://example.test"), [], state, LocalPath: path);

    private sealed class Catalog(ContentItem item) : IContentCatalog
    {
        public List<string> PlayedIds { get; } = [];
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>([item]);
        public Task<ContentItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ContentItem?>(id == item.Id ? item : null);
        public Task RecordPlaybackAsync(string id, DateTimeOffset playedAtUtc, CancellationToken cancellationToken = default) { PlayedIds.Add(id); return Task.CompletedTask; }
    }

    private sealed class Launcher(bool starts) : IPlaybackLauncher
    {
        public bool WasCalled { get; private set; }
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath, PlaybackLaunchOptions options, CancellationToken cancellationToken = default) { WasCalled = true; return Task.FromResult(starts ? new PlaybackLaunchResult(true, "VLC", "VLC") : new PlaybackLaunchResult(false, string.Empty, "Falha")); }
    }
}
