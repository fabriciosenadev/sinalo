using Sinalo.Domain;
using Sinalo.Application.Playback;

namespace Sinalo.Application.Library;

public enum LibrarySort { Name, AddedAt, UsageDate }
public sealed record LibraryQuery(string Search = "", string Origin = "Todos", LibrarySort Sort = LibrarySort.Name, int Page = 0, int PageSize = 50);
public sealed record LibraryPage(IReadOnlyList<LibraryMedia> Items, int Total);
public interface ILibraryRepository
{
    Task<LibraryPage> QueryAsync(LibraryQuery query, CancellationToken token = default);
    Task<LibraryMedia?> FindAsync(string id, CancellationToken token = default);
    Task<LibraryMedia?> FindByPathAsync(string path, CancellationToken token = default);
    Task SaveImportedAsync(LibraryMedia media, CancellationToken token = default);
    Task RenameAsync(string id, string name, DateOnly? usageDate, CancellationToken token = default);
    Task RelinkAsync(string id, string path, long size, string hash, CancellationToken token = default);
    Task RemoveImportedAsync(string id, CancellationToken token = default);
    Task RecordPlaybackAsync(string id, CancellationToken token = default);
    Task RelocateAsync(string previousRoot, string nextRoot, CancellationToken token = default);
    Task<int> CountPathReferencesAsync(string path, CancellationToken token = default);
}
public interface IMediaClassifier { bool Supports(string path); }
public interface ILocalMediaValidator { Task ValidateAsync(string path, CancellationToken token = default); }
public sealed record LocalMediaMetadata(double? DurationSeconds = null, int? Width = null, int? Height = null);
public interface ILocalMediaMetadataReader { Task<LocalMediaMetadata> ReadAsync(string path, CancellationToken token = default); }
public sealed record LibraryImportRequest(IReadOnlyList<string> Paths, MediaStorageMode StorageMode = MediaStorageMode.Managed, bool IncludeSubfolders = true);
public sealed record LibraryImportProgress(string File, string Stage, double Percentage);
public sealed record LibraryImportResult(int Imported, int Existing, int Ignored, IReadOnlyList<string> Failures, bool Cancelled);
public interface ILibraryImportService
{
    Task<LibraryImportResult> ImportAsync(LibraryImportRequest request, IProgress<LibraryImportProgress>? progress = null, CancellationToken token = default);
    Task RecoverAsync(CancellationToken token = default);
}
public interface ILibraryFileService
{
    Task LocateAsync(string id, string replacementPath, CancellationToken token = default);
    Task RemoveAsync(string id, bool deleteManagedCopy, CancellationToken token = default);
}
public sealed class LibraryPlaybackService(ILibraryRepository library, Sinalo.Application.Catalog.IContentCatalog catalog,
    IPlaybackLauncher launcher, ILocalMediaValidator validator)
{
    public async Task<PlaybackResult> PlayAsync(string id, PlaybackLaunchOptions options, CancellationToken token = default)
    {
        var media = await library.FindAsync(id, token);
        if (media is null || !media.CanPlay) return new(false, "Localize e valide o arquivo antes de reproduzir.");
        await Task.Run(() => validator.ValidateAsync(media.LocalPath!, token), token);
        if (media.ContentItemId is { } linked) return await new PlaybackService(catalog, launcher).PlayAsync(linked, options, token);
        var result = await launcher.LaunchAsync(media.LocalPath!, options, token);
        if (result.Started) await library.RecordPlaybackAsync(id, token);
        return new(result.Started, result.Message);
    }
}
