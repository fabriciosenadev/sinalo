using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Application.Playback;
using Sinalo.Application.Library;

namespace Sinalo.Infrastructure;

public sealed class LocalContentDeletionService(IContentCatalog catalog, ISinaloPathService paths,
    PlaybackActivityGate? activity = null, ContentOperationGate? operations = null, ILibraryRepository? library = null) : IContentDeletionService
{
    public async Task DeleteAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        if (activity?.IsActive == true) throw new InvalidOperationException("Encerre a reprodução antes de excluir vídeos.");
        using var lease = operations is null ? null : await operations.EnterAsync(cancellationToken);
        if (activity?.IsActive == true) throw new InvalidOperationException("Encerre a reprodução antes de excluir vídeos.");
        var item = await catalog.FindByIdAsync(contentItemId, cancellationToken)
            ?? throw new InvalidOperationException("O vídeo não foi encontrado no catálogo.");
        if (item.SyncState != SyncState.Ready || string.IsNullOrWhiteSpace(item.LocalPath))
            throw new InvalidOperationException("Somente vídeos disponíveis offline podem ser excluídos.");

        var contentRoot = Path.GetFullPath(paths.GetPaths().ContentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(item.LocalPath);
        if (!filePath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O arquivo do vídeo está fora da pasta de conteúdo do Sinalo.");

        if (library is not null && await library.CountPathReferencesAsync(filePath, cancellationToken) > 1) throw new InvalidOperationException("Outro cadastro depende deste arquivo.");
        if (File.Exists(filePath)) File.Delete(filePath);
        await catalog.DeleteAsync(contentItemId, cancellationToken);
    }
}
