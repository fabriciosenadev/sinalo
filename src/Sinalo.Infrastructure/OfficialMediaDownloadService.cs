using System.Security.Cryptography;
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
        var targetDirectory = Path.Combine(p.ContentPath, item.Quarter.ToString(), "provai-e-vede"); Directory.CreateDirectory(targetDirectory);
        var part = Path.Combine(p.TempDownloadsPath, asset.Id + ".part"); var target = Path.Combine(targetDirectory, fileName);
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
        if (new FileInfo(part).Length == 0) { File.Delete(part); throw new InvalidDataException("O arquivo baixado está vazio."); }
        progress?.Report(new DownloadProgress(item, new FileInfo(part).Length, totalBytes, "Validando arquivo"));
        string hash;
        await using (var hashStream = File.OpenRead(part)) hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();
        File.Move(part, target, true);
        var completed = item with { Assets = [asset with { ExpectedSizeBytes = new FileInfo(target).Length, Sha256 = hash }], SyncState = SyncState.Ready, LocalPath = target };
        progress?.Report(new DownloadProgress(completed, new FileInfo(target).Length, new FileInfo(target).Length, "Disponível offline"));
        return completed;
    }
}
