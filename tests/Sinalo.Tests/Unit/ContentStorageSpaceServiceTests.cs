using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.IO;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class ContentStorageSpaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sinalo-space-" + Guid.NewGuid());

    [Fact]
    public async Task AssessAsync_ShouldApplyOneGigabyteMinimumSafetyMargin()
    {
        var service = CreateService();
        var assessment = await service.AssessAsync([Item(ContentSource.Health, 1024)]);

        Assert.Equal(1024, assessment.KnownDownloadBytes);
        Assert.Equal(1024L * 1024 * 1024 + 1024, assessment.RequiredBytes);
        Assert.False(assessment.HasUnknownSizes);
    }

    [Fact]
    public async Task AssessAsync_ShouldReserveTemporaryExtractionSpaceForMissions()
    {
        var service = CreateService();
        var assessment = await service.AssessAsync([Item(ContentSource.Missions, 4096)]);

        Assert.Equal(8192, assessment.KnownDownloadBytes);
        Assert.Equal(1024L * 1024 * 1024 + 8192, assessment.RequiredBytes);
    }

    [Fact]
    public async Task AssessAsync_ShouldUseHeadWhenTheDiscoveredPageDoesNotExposeSize()
    {
        var service = CreateService(contentLength: 777);
        var assessment = await service.AssessAsync([Item(ContentSource.ProvaiEVede, null)]);

        Assert.Equal(777, assessment.KnownDownloadBytes);
        Assert.False(assessment.HasUnknownSizes);
    }

    [Fact]
    public async Task AssessAsync_ShouldWarnWhenSizeCannotBeResolved()
    {
        var service = CreateService(contentLength: null);
        var assessment = await service.AssessAsync([Item(ContentSource.ProvaiEVede, null)]);

        Assert.Equal(1, assessment.UnknownItemCount);
        Assert.Equal(1024L * 1024 * 1024, assessment.RequiredBytes);
    }

    private ContentStorageSpaceService CreateService(long? contentLength = 0) => new(new HttpClient(new HeadHandler(contentLength)), new TestPaths(_root));

    private static ContentItem Item(ContentSource source, long? size) => new("item", source, "Vídeo", new DateOnly(2026, 8, 8), new Uri("https://example.test/page"), [new MediaAsset("asset", new Uri("https://example.test/video.mp4"), "video.mp4", size, null)]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class HeadHandler(long? contentLength) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (contentLength is null) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentLength = contentLength;
            return Task.FromResult(response);
        }
    }

    private sealed class TestPaths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp"), Path.Combine(root, "data", "db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders() => Directory.CreateDirectory(_paths.ContentPath);
    }
}
