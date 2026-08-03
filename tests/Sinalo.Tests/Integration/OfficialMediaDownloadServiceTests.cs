using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class OfficialMediaDownloadServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DownloadAsync_ShouldValidateAndAtomicallyPlaceAnOfficialVideo()
    {
        var paths = new TestPaths(_root);
        var service = new OfficialMediaDownloadService(new HttpClient(new BytesHandler([1, 2, 3, 4])), paths);
        var item = Item(expectedSize: 4);

        var ready = await service.DownloadAsync(item);

        Assert.Equal(SyncState.Ready, ready.SyncState);
        Assert.NotNull(ready.LocalPath);
        Assert.True(File.Exists(ready.LocalPath));
        Assert.Equal(4, new FileInfo(ready.LocalPath).Length);
        Assert.False(File.Exists(Path.Combine(paths.GetPaths().TempDownloadsPath, "asset-1.part")));
        Assert.Equal(64, ready.Assets.Single().Sha256!.Length);
    }

    [Fact]
    public async Task DownloadAsync_ShouldRemoveTheTemporaryFileWhenValidationFails()
    {
        var paths = new TestPaths(_root);
        var service = new OfficialMediaDownloadService(new HttpClient(new BytesHandler([1, 2, 3])), paths);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(Item(expectedSize: 4)));

        Assert.False(File.Exists(Path.Combine(paths.GetPaths().TempDownloadsPath, "asset-1.part")));
    }

    private static ContentItem Item(long expectedSize) => new("provai-1", ContentSource.ProvaiEVede, "Provai", new DateOnly(2026, 8, 8), new Uri("https://example.test/page"), [new MediaAsset("asset-1", new Uri("https://example.test/video.mp4"), "video.mp4", expectedSize, null)]);

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class BytesHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    private sealed class TestPaths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp", "downloads"), Path.Combine(root, "data", "sinalo.db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders() { Directory.CreateDirectory(_paths.ContentPath); Directory.CreateDirectory(_paths.TempDownloadsPath); }
    }
}
