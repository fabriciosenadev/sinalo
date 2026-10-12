using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class LocalContentPathMigrationService(
    IContentPathConfigurationService configuration,
    IContentCatalog catalog, ILibraryRepository? library = null, ContentOperationGate? operations = null,
    PlaybackActivityGate? activity = null) : IContentPathMigrationService
{
    public Task MoveAsync(string newContentPath, CancellationToken cancellationToken = default) => Task.Run(() => MoveCoreAsync(newContentPath, cancellationToken));
    private async Task MoveCoreAsync(string newContentPath, CancellationToken cancellationToken)
    {
        if (activity?.IsActive == true) throw new InvalidOperationException("Encerre a reprodução antes de mover o conteúdo.");
        using var lease = operations is null ? null : await operations.EnterAsync(cancellationToken);
        if (activity?.IsActive == true) throw new InvalidOperationException("Encerre a reprodução antes de mover o conteúdo.");
        var previousPath = Normalize(configuration.GetContentPath());
        var targetPath = Normalize(newContentPath);
        if (string.Equals(previousPath, targetPath, StringComparison.OrdinalIgnoreCase)) return;
        if (targetPath.StartsWith(previousPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A nova pasta não pode ficar dentro da pasta de conteúdo atual.");

        var candidates = Directory.Exists(previousPath)
            ? Directory.EnumerateFiles(previousPath, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }).ToArray()
            : [];
        var files = new List<string>();
        foreach (var file in candidates)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || Path.GetRelativePath(previousPath, file).Split(Path.DirectorySeparatorChar).Any(part => part is ".incoming" or ".sinalo-imports")) continue;
            if (library is not null && await library.FindByPathAsync(file, cancellationToken) is { IsImported: true, StorageMode: MediaStorageMode.Referenced }) continue;
            files.Add(file);
        }
        EnsureAvailableSpace(targetPath, files);

        foreach (var sourcePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(previousPath, sourcePath);
            var targetFilePath = Path.Combine(targetPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            if (File.Exists(targetFilePath))
            {
                var gate = activity ?? new PlaybackActivityGate();
                if (new FileInfo(sourcePath).Length != new FileInfo(targetFilePath).Length ||
                    await LocalLibraryImportService.CopyVerifiedAsync(sourcePath, null, gate, cancellationToken) != await LocalLibraryImportService.CopyVerifiedAsync(targetFilePath, null, gate, cancellationToken))
                    throw new IOException($"Já existe um arquivo diferente em '{targetFilePath}'. Escolha outra pasta ou remova o arquivo conflitante.");
                continue;
            }

            var temporary = targetFilePath + $".migration-{Guid.NewGuid():N}.part";
            try
            {
                await LocalLibraryImportService.CopyVerifiedAsync(sourcePath, temporary, activity ?? new PlaybackActivityGate(), cancellationToken);
                File.Move(temporary, targetFilePath, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        try
        {
            await catalog.RelocateLocalPathsAsync(previousPath, targetPath, cancellationToken);
            if (library is not null) await library.RelocateAsync(previousPath, targetPath, cancellationToken);
            configuration.SaveContentPath(targetPath);
        }
        catch
        {
            await catalog.RelocateLocalPathsAsync(targetPath, previousPath, CancellationToken.None);
            if (library is not null) await library.RelocateAsync(targetPath, previousPath, CancellationToken.None);
            throw;
        }

        foreach (var sourcePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var destination = Path.Combine(targetPath, Path.GetRelativePath(previousPath, sourcePath));
                if (File.Exists(sourcePath) && await LocalLibraryImportService.CopyVerifiedAsync(sourcePath, null, activity ?? new PlaybackActivityGate(), cancellationToken) ==
                    await LocalLibraryImportService.CopyVerifiedAsync(destination, null, activity ?? new PlaybackActivityGate(), cancellationToken)) File.Delete(sourcePath);
            }
            catch (IOException)
            {
                // O destino já está pronto e passa a ser a biblioteca ativa.
                // Um arquivo que esteja em uso pode permanecer na pasta antiga temporariamente.
            }
        }

        DeleteEmptyDirectories(previousPath);
    }

    private static void EnsureAvailableSpace(string targetPath, IReadOnlyList<string> files)
    {
        if (files.Count == 0) return;
        var requiredBytes = files.Sum(path => new FileInfo(path).Length);
        var root = Path.GetPathRoot(targetPath);
        if (string.IsNullOrWhiteSpace(root)) return;
        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < requiredBytes)
            throw new IOException("Não há espaço livre suficiente na nova pasta para transferir os vídeos.");
    }

    private static void DeleteEmptyDirectories(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return;
        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).OrderByDescending(path => path.Length))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
    }

    private static string Normalize(string path) => Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
