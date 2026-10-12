using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class LocalLibraryFileService(ILibraryRepository library, ISinaloPathService paths,
    ILocalMediaValidator validator, ContentOperationGate operations, PlaybackActivityGate activity) : ILibraryFileService
{
    public Task LocateAsync(string id, string replacementPath, CancellationToken token = default) => Task.Run(() => LocateCoreAsync(id, replacementPath, token));
    private async Task LocateCoreAsync(string id, string replacementPath, CancellationToken token)
    {
        RequireIdle();
        using var lease = await operations.EnterAsync(token);
        RequireIdle();
        var media = await library.FindAsync(id, token) ?? throw new InvalidOperationException("O cadastro não foi encontrado.");
        var replacement = Path.GetFullPath(replacementPath);
        await validator.ValidateAsync(replacement, token);
        var target = replacement;
        var createdCopy = false;
        try
        {
        if (media.StorageMode == MediaStorageMode.Managed)
        {
            if (media.IsImported)
            {
                if (!media.Id.StartsWith("local-", StringComparison.Ordinal) || !Guid.TryParseExact(media.Id[6..], "N", out _))
                    throw new InvalidOperationException("O identificador da cópia local é inválido.");
                target = Path.Combine(paths.GetPaths().ContentPath, "imported", media.Id, Path.GetFileName(replacement));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!string.Equals(target, replacement, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(target)) throw new IOException("Já existe uma cópia neste destino. Ela não será sobrescrita.");
                    var part = target + ".locating.part";
                    try { await LocalLibraryImportService.CopyVerifiedAsync(replacement, part, activity, token); File.Move(part, target, false); createdCopy = true; }
                    finally { if (File.Exists(part)) File.Delete(part); }
                }
            }
            else if (!Owned(target)) throw new InvalidOperationException("Para este vídeo do programa, escolha um arquivo dentro da pasta de conteúdo do Sinalo.");
        }
        var hash = await LocalLibraryImportService.CopyVerifiedAsync(target, null, activity, token);
        await library.RelinkAsync(id, target, new FileInfo(target).Length, hash, token);
        }
        catch
        {
            if (createdCopy && File.Exists(target))
            {
                var persisted = await library.FindAsync(id, CancellationToken.None);
                if (!string.Equals(persisted?.LocalPath, target, StringComparison.OrdinalIgnoreCase)) File.Delete(target);
            }
            throw;
        }
    }
    public async Task RemoveAsync(string id, bool deleteManagedCopy, CancellationToken token = default)
    {
        RequireIdle();
        using var lease = await operations.EnterAsync(token);
        RequireIdle();
        var media = await library.FindAsync(id, token) ?? throw new InvalidOperationException("O cadastro não foi encontrado.");
        if (!media.IsImported) throw new InvalidOperationException("Use a exclusão do programa para vídeos sincronizados.");
        if (deleteManagedCopy)
        {
            if (media.StorageMode != MediaStorageMode.Managed || media.LocalPath is null || !Owned(media.LocalPath))
                throw new InvalidOperationException("O Sinalo não pode apagar o arquivo original referenciado.");
            if (await library.CountPathReferencesAsync(media.LocalPath, token) > 1) throw new InvalidOperationException("Outro cadastro depende deste arquivo.");
            // Remove the record only after the managed copy can be deleted successfully.
            if (File.Exists(media.LocalPath)) File.Delete(media.LocalPath);
        }
        await library.RemoveImportedAsync(id, token);
    }
    private bool Owned(string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(paths.GetPaths().ContentPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private void RequireIdle() { if (activity.IsActive) throw new InvalidOperationException("Encerre a reprodução antes de localizar, excluir ou mover arquivos."); }
}
