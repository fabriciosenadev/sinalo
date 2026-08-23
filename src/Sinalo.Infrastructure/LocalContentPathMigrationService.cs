using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;

namespace Sinalo.Infrastructure;

public sealed class LocalContentPathMigrationService(
    IContentPathConfigurationService configuration,
    IContentCatalog catalog) : IContentPathMigrationService
{
    public async Task MoveAsync(string newContentPath, CancellationToken cancellationToken = default)
    {
        var previousPath = Normalize(configuration.GetContentPath());
        var targetPath = Normalize(newContentPath);
        if (string.Equals(previousPath, targetPath, StringComparison.OrdinalIgnoreCase)) return;
        if (targetPath.StartsWith(previousPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A nova pasta não pode ficar dentro da pasta de conteúdo atual.");

        var files = Directory.Exists(previousPath)
            ? Directory.EnumerateFiles(previousPath, "*", SearchOption.AllDirectories).ToArray()
            : [];
        EnsureAvailableSpace(targetPath, files);

        foreach (var sourcePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(previousPath, sourcePath);
            var targetFilePath = Path.Combine(targetPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            if (File.Exists(targetFilePath))
            {
                if (new FileInfo(sourcePath).Length != new FileInfo(targetFilePath).Length)
                    throw new IOException($"Já existe um arquivo diferente em '{targetFilePath}'. Escolha outra pasta ou remova o arquivo conflitante.");
                continue;
            }

            await CopyAsync(sourcePath, targetFilePath, cancellationToken);
        }

        await catalog.RelocateLocalPathsAsync(previousPath, targetPath, cancellationToken);
        configuration.SaveContentPath(targetPath);

        foreach (var sourcePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(sourcePath)) File.Delete(sourcePath);
            }
            catch (IOException)
            {
                // O destino já está pronto e passa a ser a biblioteca ativa.
                // Um arquivo que esteja em uso pode permanecer na pasta antiga temporariamente.
            }
        }

        DeleteEmptyDirectories(previousPath);
    }

    private static async Task CopyAsync(string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
        await source.CopyToAsync(target, cancellationToken);
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
        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
    }

    private static string Normalize(string path) => Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
