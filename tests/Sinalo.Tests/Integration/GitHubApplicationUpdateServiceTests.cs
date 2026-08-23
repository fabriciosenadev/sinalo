using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class GitHubApplicationUpdateServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ShouldDiscoverAndDownloadNewReleaseWithChecksum()
    {
        var installer = Encoding.UTF8.GetBytes("installer-content");
        var hash = Convert.ToHexString(SHA256.HashData(installer));
        using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/fabriciosenadev/sinalo/releases/latest" => Json($"{{\"tag_name\":\"v0.1.5\",\"body\":\"Atualização\",\"assets\":[{{\"name\":\"Sinalo-Setup-win-x64.exe\",\"browser_download_url\":\"https://example.test/setup.exe\"}},{{\"name\":\"Sinalo-Setup-win-x64.exe.sha256\",\"browser_download_url\":\"https://example.test/setup.sha256\"}}]}}"),
            "/setup.exe" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) },
            "/setup.sha256" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{hash}  Sinalo-Setup-win-x64.exe") },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        }));
        var service = new GitHubApplicationUpdateService(client, new UpdatePaths(_rootPath));

        var update = await service.CheckAsync(new Version(0, 1, 4));
        var downloaded = await service.DownloadAsync(update!);

        Assert.Equal(new Version(0, 1, 5), update!.Version);
        Assert.Equal(installer, await File.ReadAllBytesAsync(downloaded.InstallerPath));
    }

    [Fact]
    public async Task ShouldIgnoreCurrentRelease()
    {
        using var client = new HttpClient(new Handler(_ => Json("{\"tag_name\":\"v0.1.4\",\"assets\":[]}")));
        var service = new GitHubApplicationUpdateService(client, new UpdatePaths(_rootPath));

        Assert.Null(await service.CheckAsync(new Version(0, 1, 4)));
    }

    [Fact]
    public async Task ShouldIgnoreReleaseWithoutInstaller()
    {
        using var client = new HttpClient(new Handler(_ => Json("{\"tag_name\":\"v0.1.5\",\"assets\":[]}")));
        var service = new GitHubApplicationUpdateService(client, new UpdatePaths(_rootPath));

        Assert.Null(await service.CheckAsync(new Version(0, 1, 4)));
    }

    [Fact]
    public async Task ShouldRejectInstallerWithInvalidChecksum()
    {
        var installer = Encoding.UTF8.GetBytes("installer-content");
        using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/fabriciosenadev/sinalo/releases/latest" => Json("{\"tag_name\":\"v0.1.5\",\"assets\":[{\"name\":\"Sinalo-Setup-win-x64.exe\",\"browser_download_url\":\"https://example.test/setup.exe\"},{\"name\":\"Sinalo-Setup-win-x64.exe.sha256\",\"browser_download_url\":\"https://example.test/setup.sha256\"}]}"),
            "/setup.exe" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) },
            "/setup.sha256" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0000") },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        }));
        var service = new GitHubApplicationUpdateService(client, new UpdatePaths(_rootPath));
        var update = await service.CheckAsync(new Version(0, 1, 4));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(update!));
    }

    [Fact]
    public async Task ShouldUseGitHubDigestWhenChecksumAssetIsUnavailable()
    {
        var installer = Encoding.UTF8.GetBytes("installer-content");
        var hash = Convert.ToHexString(SHA256.HashData(installer));
        using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/fabriciosenadev/sinalo/releases/latest" => Json($"{{\"tag_name\":\"v0.1.5\",\"assets\":[{{\"name\":\"Sinalo-Setup-win-x64.exe\",\"browser_download_url\":\"https://example.test/setup.exe\",\"digest\":\"sha256:{hash}\"}}]}}"),
            "/setup.exe" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        }));
        var service = new GitHubApplicationUpdateService(client, new UpdatePaths(_rootPath));
        var update = await service.CheckAsync(new Version(0, 1, 4));

        Assert.True(File.Exists((await service.DownloadAsync(update!)).InstallerPath));
    }

    public void Dispose() { if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, true); }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }

    private sealed class UpdatePaths(string root) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(root, Path.Combine(root, "data"), Path.Combine(root, "content"), Path.Combine(root, "cache"), Path.Combine(root, "logs"), Path.Combine(root, "temp"), Path.Combine(root, "data", "sinalo.db"));
        public SinaloPaths GetPaths() => _paths;
        public void EnsureFolders() => Directory.CreateDirectory(_paths.RootPath);
    }
}
