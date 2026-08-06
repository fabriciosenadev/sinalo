using System.Security.Cryptography;
using System.IO.Compression;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
namespace Sinalo.Infrastructure;
public sealed class OfficialMediaDownloadService(HttpClient httpClient, ISinaloPathService paths) : IContentDownloadService
{
    public async Task<ContentItem> DownloadAsync(ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var asset = item.Assets.SingleOrDefault() ?? throw new InvalidOperationException("O item não possui arquivo oficial.");
        paths.EnsureFolders(); var p = paths.GetPaths(); var fileName = Path.GetFileName(asset.FileName);
        if (string.IsNullOrWhiteSpace(fileName) || fileName != asset.FileName) throw new InvalidOperationException("Nome de arquivo inválido.");
        var targetDirectory = Path.Combine(p.ContentPath, item.Quarter.ToString(), GetSourceFolder(item.Source)); Directory.CreateDirectory(targetDirectory);
        var part = Path.Combine(p.TempDownloadsPath, asset.Id + ".part"); var target = Path.Combine(targetDirectory, fileName);
        var extractedPart = Path.Combine(p.TempDownloadsPath, asset.Id + ".extracted.part");
        try
        {
            using var response = await httpClient.GetAsync(asset.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken); response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            progress?.Report(new DownloadProgress(item, 0, totalBytes, "Baixando"));
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(part))
            {
                var buffer = new byte[64 * 1024];
                long received = 0;
                long lastReported = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    received += count;
                    // Atualiza a tela em blocos: progresso útil sem inundar a thread da interface.
                    if (received - lastReported >= 256 * 1024 || received == totalBytes)
                    {
                        progress?.Report(new DownloadProgress(item, received, totalBytes, "Baixando"));
                        lastReported = received;
                    }
                }
            }
            if (new FileInfo(part).Length == 0) throw new InvalidDataException("O arquivo baixado está vazio.");

            var localVideo = part;
            if (await IsZipAsync(part, response.Content.Headers.ContentType?.MediaType, cancellationToken))
            {
                progress?.Report(new DownloadProgress(item, new FileInfo(part).Length, totalBytes, "Extraindo vídeo"));
                localVideo = await ExtractVideoAsync(part, extractedPart, cancellationToken);
            }

            progress?.Report(new DownloadProgress(item, new FileInfo(localVideo).Length, new FileInfo(localVideo).Length, "Validando arquivo"));
            string hash;
            await using (var hashStream = File.OpenRead(localVideo)) hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();
            File.Move(localVideo, target, true);
            var ready = item with { Assets = [asset with { ExpectedSizeBytes = new FileInfo(target).Length, Sha256 = hash }], SyncState = SyncState.Ready, LocalPath = target };
            progress?.Report(new DownloadProgress(ready, new FileInfo(target).Length, new FileInfo(target).Length, "Disponível offline"));
            return ready;
        }
        finally
        {
            if (File.Exists(part)) File.Delete(part);
            if (File.Exists(extractedPart)) File.Delete(extractedPart);
        }
    }

    private static async Task<bool> IsZipAsync(string filePath, string? mediaType, CancellationToken cancellationToken)
    {
        if (mediaType?.Contains("zip", StringComparison.OrdinalIgnoreCase) == true) return true;
        var signature = new byte[4];
        await using var input = File.OpenRead(filePath);
        var read = await input.ReadAsync(signature, cancellationToken);
        return read == 4 && signature[0] == 0x50 && signature[1] == 0x4B &&
            ((signature[2] == 0x03 && signature[3] == 0x04) || (signature[2] == 0x05 && signature[3] == 0x06));
    }

    private static async Task<string> ExtractVideoAsync(string archivePath, string extractedPath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var video = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) && entry.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Length)
            .FirstOrDefault() ?? throw new InvalidDataException("O arquivo ZIP não contém um vídeo MP4.");

        await using (var input = video.Open())
        await using (var output = File.Create(extractedPath))
        {
            await input.CopyToAsync(output, cancellationToken);
        }
        if (new FileInfo(extractedPath).Length == 0) throw new InvalidDataException("O vídeo extraído está vazio.");
        return extractedPath;
    }

    private static string GetSourceFolder(ContentSource source) => source switch
    {
        ContentSource.Missions => "missions",
        ContentSource.ProvaiEVede => "provai-e-vede",
        ContentSource.Health => "health",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
