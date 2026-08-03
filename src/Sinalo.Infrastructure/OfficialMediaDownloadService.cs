using System.Security.Cryptography;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class OfficialMediaDownloadService(HttpClient httpClient, ISinaloPathService pathService) : IContentDownloadService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ISinaloPathService _pathService = pathService;

    public async Task<ContentItem> DownloadAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        var asset = item.Assets.SingleOrDefault() ?? throw new InvalidOperationException("O item não possui arquivo oficial para sincronização.");
        _pathService.EnsureFolders();
        var paths = _pathService.GetPaths();
        var fileName = Path.GetFileName(asset.FileName);
        if (string.IsNullOrWhiteSpace(fileName) || fileName != asset.FileName) throw new InvalidOperationException("Nome de arquivo inválido.");

        var destinationDirectory = Path.Combine(paths.ContentPath, item.Quarter.ToString(), "provai-e-vede");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(paths.TempDownloadsPath, $"{asset.Id}.part");
        var destinationPath = Path.Combine(destinationDirectory, fileName);

        using var response = await _httpClient.GetAsync(asset.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = File.Create(temporaryPath))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        var length = new FileInfo(temporaryPath).Length;
        if (length == 0 || (asset.ExpectedSizeBytes is long expectedSize && length != expectedSize))
        {
            File.Delete(temporaryPath);
            throw new InvalidDataException("O arquivo baixado não passou na validação de tamanho.");
        }

        string sha256;
        await using (var stream = File.OpenRead(temporaryPath)) sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        File.Move(temporaryPath, destinationPath, overwrite: true);

        var validatedAsset = asset with { ExpectedSizeBytes = length, Sha256 = sha256 };
        return item with { Assets = [validatedAsset], SyncState = SyncState.Ready, LocalPath = destinationPath };
    }
}
