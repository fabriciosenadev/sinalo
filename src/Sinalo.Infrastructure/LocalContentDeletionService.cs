using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class LocalContentDeletionService(IContentCatalog catalog, ISinaloPathService paths) : IContentDeletionService
{
    public async Task DeleteAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var item = await catalog.FindByIdAsync(contentItemId, cancellationToken)
            ?? throw new InvalidOperationException("O vídeo não foi encontrado no catálogo.");
        if (item.SyncState != SyncState.Ready || string.IsNullOrWhiteSpace(item.LocalPath))
            throw new InvalidOperationException("Somente vídeos disponíveis offline podem ser excluídos.");

        var contentRoot = Path.GetFullPath(paths.GetPaths().ContentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(item.LocalPath);
        if (!filePath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O arquivo do vídeo está fora da pasta de conteúdo do Sinalo.");

        if (File.Exists(filePath)) File.Delete(filePath);
        await catalog.DeleteAsync(contentItemId, cancellationToken);
    }
}
