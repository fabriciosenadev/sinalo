using System.Net;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;
namespace Sinalo.Tests.Integration;
public sealed class OfficialMediaDownloadServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));
    [Fact] public async Task DownloadAsync_ShouldSaveHashAndMarkItemReady()
    {
        var paths = new TestPaths(_root); var service = new OfficialMediaDownloadService(new HttpClient(new Bytes([1,2,3])), paths);
        var ready = await service.DownloadAsync(Item());
        Assert.Equal(SyncState.Ready, ready.SyncState); Assert.True(File.Exists(ready.LocalPath)); Assert.Equal(64, ready.Assets.Single().Sha256!.Length); Assert.False(File.Exists(Path.Combine(paths.GetPaths().TempDownloadsPath, "asset.part")));
    }
    [Fact] public async Task DownloadAsync_ShouldRejectMissingAssetAndEmptyFile()
    {
        var paths = new TestPaths(_root); var empty = new OfficialMediaDownloadService(new HttpClient(new Bytes([])), paths);
        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.DownloadAsync(Item() with { Assets = [] }));
        await Assert.ThrowsAsync<InvalidDataException>(() => empty.DownloadAsync(Item()));
    }
    [Fact] public async Task DownloadAsync_ShouldStoreMissionsInItsOwnSourceFolder()
    {
        var paths = new TestPaths(_root); var service = new OfficialMediaDownloadService(new HttpClient(new Bytes([1])), paths);
        var ready = await service.DownloadAsync(Item() with { Source = ContentSource.Missions });
        Assert.Contains(Path.Combine("2026-T3", "missions"), ready.LocalPath!, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task DownloadAsync_ShouldExtractTheVideoWhenTheOfficialEndpointReturnsZip()
    {
        var video = new byte[] { 4, 5, 6, 7 };
        var paths = new TestPaths(_root); var service = new OfficialMediaDownloadService(new HttpClient(new Bytes(CreateZip(video), "application/zip")), paths);

        var ready = await service.DownloadAsync(Item() with { Source = ContentSource.Missions });

        Assert.Equal(video, await File.ReadAllBytesAsync(ready.LocalPath!));
        Assert.EndsWith(".mp4", ready.LocalPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(paths.GetPaths().TempDownloadsPath, "asset.part")));
        Assert.False(File.Exists(Path.Combine(paths.GetPaths().TempDownloadsPath, "asset.extracted.part")));
    }
    private static ContentItem Item() => new("item", ContentSource.ProvaiEVede, "Vídeo", new DateOnly(2026,8,8), new Uri("https://example.test/page"), [new MediaAsset("asset",new Uri("https://example.test/video.mp4"),"video.mp4",null,null)]);
    public void Dispose(){ if(Directory.Exists(_root)) Directory.Delete(_root,true); }
    private static byte[] CreateZip(byte[] video)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var output = archive.CreateEntry("informativo.mp4").Open()) output.Write(video);
        return stream.ToArray();
    }
    private sealed class Bytes(byte[] data, string? mediaType = null):HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){ var content = new ByteArrayContent(data); if (mediaType is not null) content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content}); } }
    private sealed class TestPaths(string root):ISinaloPathService { private readonly SinaloPaths _p=new(root,Path.Combine(root,"data"),Path.Combine(root,"content"),Path.Combine(root,"cache"),Path.Combine(root,"logs"),Path.Combine(root,"temp","downloads"),Path.Combine(root,"data","db")); public SinaloPaths GetPaths()=>_p; public void EnsureFolders(){Directory.CreateDirectory(_p.ContentPath);Directory.CreateDirectory(_p.TempDownloadsPath);} }
}
