using System.Security.Cryptography;
using System.Text.Json;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class LocalLibraryImportService(ISinaloPathService paths, ILibraryRepository library,
    IMediaClassifier classifier, ILocalMediaValidator validator, ContentOperationGate operations,
    PlaybackActivityGate activity, Func<string, long>? freeSpace = null) : ILibraryImportService
{
    private readonly Func<string, long> _freeSpace = freeSpace ?? (path => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace);
    private sealed record ImportJournal(string Id, string FileName);
    public Task<LibraryImportResult> ImportAsync(LibraryImportRequest request, IProgress<LibraryImportProgress>? progress = null, CancellationToken token = default) =>
        Task.Run(() => ImportCoreAsync(request, progress, token));

    private async Task<LibraryImportResult> ImportCoreAsync(LibraryImportRequest request, IProgress<LibraryImportProgress>? progress, CancellationToken token)
    {
        var imported = 0; var existing = 0; var ignored = 0; var failures = new List<string>();
        try
        {
            using var lease = await operations.EnterAsync(token);
            paths.EnsureFolders();
            await RecoverCoreAsync(token);
            progress?.Report(new("", "Enumerando arquivos", 0));
            var candidates = new List<string>();
            foreach (var file in Enumerate(request, failures, token).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!classifier.Supports(file)) { ignored++; continue; }
                if (await library.FindByPathAsync(file, token) is not null) { existing++; continue; }
                candidates.Add(file);
            }
            var files = candidates.ToArray();
            long remaining = files.Where(classifier.Supports).Sum(file => new FileInfo(file).Length);
            if (request.StorageMode == MediaStorageMode.Managed && remaining > 0) CheckSpace(remaining);
            for (var index = 0; index < files.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var file = files[index];
                if (!classifier.Supports(file)) { ignored++; continue; }
                var size = new FileInfo(file).Length;
                string? job = null; string? final = null; string? id = null;
                try
                {
                    if (await library.FindByPathAsync(file, token) is not null) { existing++; continue; }
                    if (file.StartsWith(Path.Combine(paths.GetPaths().ContentPath, "imported") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Este arquivo já está na área gerenciada. Localize seu cadastro em vez de importá-lo novamente.");
                    await activity.WaitUntilIdleAsync(token);
                    progress?.Report(new(file, "Validando vídeo", 100d * index / Math.Max(1, files.Length)));
                    await validator.ValidateAsync(file, token);
                    id = "local-" + Guid.NewGuid().ToString("N");
                    var target = file;
                    if (request.StorageMode == MediaStorageMode.Managed)
                    {
                        CheckSpace(remaining);
                        job = Path.Combine(paths.GetPaths().ContentPath, ".sinalo-imports", id[6..]);
                        Directory.CreateDirectory(job);
                        await File.WriteAllTextAsync(Path.Combine(job, "job.json"), JsonSerializer.Serialize(new ImportJournal(id, Path.GetFileName(file))), token);
                        target = Path.Combine(job, "copy.part");
                    }
                    progress?.Report(new(file, request.StorageMode == MediaStorageMode.Managed ? "Copiando e conferindo" : "Conferindo referência", 100d * index / Math.Max(1, files.Length)));
                    var hash = await CopyVerifiedAsync(file, target == file ? null : target, activity, token,
                        copied => progress?.Report(new(file, "Copiando e conferindo", 100d * (index + (size == 0 ? 0 : copied / (double)size)) / Math.Max(1, files.Length))),
                        () => { if (request.StorageMode == MediaStorageMode.Managed && _freeSpace(paths.GetPaths().ContentPath) < 32L * 1024 * 1024) throw new StorageSpaceCriticalException(); });
                    if (job is not null)
                    {
                        final = Path.Combine(paths.GetPaths().ContentPath, "imported", id, Path.GetFileName(file));
                        Directory.CreateDirectory(Path.GetDirectoryName(final)!);
                        File.Move(target, final, false);
                        target = final;
                    }
                    await library.SaveImportedAsync(new(id, Path.GetFileNameWithoutExtension(file), null, null, null, target, file,
                        request.StorageMode, MediaAvailability.Available, DateTimeOffset.UtcNow, size, hash), token);
                    imported++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
                { failures.Add($"{Path.GetFileName(file)}: {exception.Message}"); }
                finally
                {
                    remaining -= size;
                    if (job is not null)
                    {
                        // Check the committed identity before compensating; never delete an existing imported item.
                        if (final is not null && id is not null && await library.FindAsync(id, CancellationToken.None) is null && File.Exists(final)) File.Delete(final);
                        if (Directory.Exists(job)) Directory.Delete(job, true);
                    }
                }
            }
            progress?.Report(new("", "Concluído", 100));
            return new(imported, existing, ignored, failures, false);
        }
        catch (OperationCanceledException) { return new(imported, existing, ignored, failures, true); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { failures.Add(exception.Message); return new(imported, existing, ignored, failures, false); }
    }
    private void CheckSpace(long remaining)
    {
        var reserve = Math.Max(1024L * 1024 * 1024, (long)Math.Ceiling(remaining * .25));
        if (_freeSpace(paths.GetPaths().ContentPath) < checked(remaining + reserve)) throw new IOException("Não há espaço suficiente para o lote e a reserva de segurança. Escolha outra pasta ou use referências.");
    }
    private IEnumerable<string> Enumerate(LibraryImportRequest request, List<string> failures, CancellationToken token)
    {
        var pending = new Stack<string>(request.Paths);
        while (pending.TryPop(out var entry))
        {
            token.ThrowIfCancellationRequested();
            string full; FileAttributes attributes;
            try { full = Path.GetFullPath(entry); attributes = File.GetAttributes(full); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { failures.Add($"{entry}: {exception.Message}"); continue; }
            if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
            var root = paths.GetPaths().ContentPath.TrimEnd('\\', '/');
            if (new[] { ".sinalo-imports", ".incoming" }.Any(name => full.Equals(Path.Combine(root, name), StringComparison.OrdinalIgnoreCase) || full.StartsWith(Path.Combine(root, name) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
            if ((attributes & FileAttributes.Directory) == 0) { yield return full; continue; }
            string[] children;
            try { children = Directory.GetFileSystemEntries(full); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { failures.Add($"{full}: {exception.Message}"); continue; }
            foreach (var child in children)
                if (request.IncludeSubfolders || !Directory.Exists(child)) pending.Push(child);
        }
    }
    internal static async Task<string> CopyVerifiedAsync(string sourcePath, string? destination, PlaybackActivityGate activity, CancellationToken token,
        Action<long>? progress = null, Action? checkSpace = null)
    {
        var before = new FileInfo(sourcePath); var size = before.Length; var modified = before.LastWriteTimeUtc;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true))
        await using (var output = destination is null ? null : new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true))
        {
            var buffer = new byte[64 * 1024]; long copied = 0;
            while (true)
            {
                await activity.WaitUntilIdleAsync(token); checkSpace?.Invoke();
                var count = await source.ReadAsync(buffer, token); if (count == 0) break;
                hash.AppendData(buffer, 0, count);
                if (output is not null) await output.WriteAsync(buffer.AsMemory(0, count), token);
                copied += count; progress?.Invoke(copied);
            }
            if (copied != size) throw new IOException("A origem mudou durante a leitura.");
        }
        before.Refresh();
        if (before.Length != size || before.LastWriteTimeUtc != modified) throw new IOException("A origem mudou durante a importação.");
        var result = Convert.ToHexString(hash.GetHashAndReset());
        if (destination is not null && (new FileInfo(destination).Length != size || !string.Equals(result, await CopyVerifiedAsync(destination, null, activity, token), StringComparison.Ordinal)))
            throw new IOException("A cópia não passou na verificação de integridade.");
        return result;
    }
    public async Task RecoverAsync(CancellationToken token = default)
    { using var lease = await operations.EnterAsync(token); await RecoverCoreAsync(token); }
    private async Task RecoverCoreAsync(CancellationToken token)
    {
        var root = Path.Combine(paths.GetPaths().ContentPath, ".sinalo-imports");
        if (!Directory.Exists(root)) return;
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            token.ThrowIfCancellationRequested();
            var key = Path.GetFileName(directory);
            if (!Guid.TryParseExact(key, "N", out _) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
            var journalPath = Path.Combine(directory, "job.json");
            if (File.Exists(journalPath))
            {
                ImportJournal? journal;
                try { journal = JsonSerializer.Deserialize<ImportJournal>(await File.ReadAllTextAsync(journalPath, token)); }
                catch (JsonException) { continue; }
                if (journal is null || journal.Id != "local-" + key || journal.FileName != Path.GetFileName(journal.FileName)) continue;
                var target = Path.Combine(paths.GetPaths().ContentPath, "imported", journal.Id, journal.FileName);
                if (await library.FindAsync(journal.Id, token) is null && File.Exists(target)) File.Delete(target);
            }
            Directory.Delete(directory, true);
        }
    }
}
