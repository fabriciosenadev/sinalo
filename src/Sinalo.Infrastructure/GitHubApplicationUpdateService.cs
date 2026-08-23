using System.Security.Cryptography;
using System.Text.Json;
using Sinalo.Application.Storage;
using Sinalo.Application.Updates;

namespace Sinalo.Infrastructure;

public sealed class GitHubApplicationUpdateService(HttpClient httpClient, ISinaloPathService paths) : IApplicationUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/fabriciosenadev/sinalo/releases/latest";

    public async Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(LatestReleaseUrl, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (!Version.TryParse(tag, out var remoteVersion) || remoteVersion <= currentVersion) return null;
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var installer = assets.FirstOrDefault(asset => string.Equals(asset.GetProperty("name").GetString(), "Sinalo-Setup-win-x64.exe", StringComparison.OrdinalIgnoreCase));
        if (installer.ValueKind == JsonValueKind.Undefined) return null;
        var checksum = assets.FirstOrDefault(asset => string.Equals(asset.GetProperty("name").GetString(), "Sinalo-Setup-win-x64.exe.sha256", StringComparison.OrdinalIgnoreCase));
        var installerUri = new Uri(installer.GetProperty("browser_download_url").GetString()!);
        var checksumUri = checksum.ValueKind == JsonValueKind.Undefined ? null : new Uri(checksum.GetProperty("browser_download_url").GetString()!);
        var digest = installer.TryGetProperty("digest", out var digestProperty) ? digestProperty.GetString() : null;
        return new AvailableUpdate(remoteVersion, root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty, installerUri, checksumUri, digest);
    }

    public async Task<DownloadedUpdate> DownloadAsync(AvailableUpdate update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(paths.GetPaths().RootPath, "updates");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, $"Sinalo-Setup-win-x64-{update.Version}.exe");
        var part = target + ".part";
        using var response = await httpClient.GetAsync(update.InstallerUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
        {
            var buffer = new byte[1024 * 1024]; long received = 0; int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                progress?.Report(new UpdateDownloadProgress(received, response.Content.Headers.ContentLength));
            }
        }
        var expectedHash = await GetExpectedHashAsync(update, cancellationToken);
        string actualHash;
        await using (var hashStream = File.OpenRead(part))
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken));
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)) { File.Delete(part); throw new InvalidDataException("A validação do instalador falhou."); }
        File.Move(part, target, true);
        return new DownloadedUpdate(update, target);
    }

    private async Task<string> GetExpectedHashAsync(AvailableUpdate update, CancellationToken cancellationToken)
    {
        if (update.ChecksumUri is not null)
        {
            var content = await httpClient.GetStringAsync(update.ChecksumUri, cancellationToken);
            var hash = content.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(hash)) return hash;
        }
        if (!string.IsNullOrWhiteSpace(update.Digest) && update.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return update.Digest[7..];
        throw new InvalidDataException("A release não possui checksum para validação do instalador.");
    }
}
