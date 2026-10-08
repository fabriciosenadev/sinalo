using System.Buffers.Binary;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class LibraryIntegrationTests : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));
    private LocalSinaloPathService Paths => new(rootPath: Root);
    private SqliteLibraryRepository Library => new(Paths);
    private readonly PlaybackActivityGate _activity = new();
    private readonly ContentOperationGate _operations = new();
    private LocalLibraryImportService Importer(Func<string,long>? space = null, ILibraryRepository? repository = null) => new(Paths, repository ?? Library, new Mp4LibraryValidator(_activity), new Mp4LibraryValidator(_activity), _operations, _activity, space);
    private LocalLibraryFileService Files => new(Library, Paths, new Mp4LibraryValidator(_activity), _operations, _activity);
    internal async Task InitializeAsync() => await new SinaloDatabase(Paths).InitializeAsync();
    internal string Video(string relative, int payload = 32)
    {
        var path = Path.Combine(Root, "originals", relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        foreach (var (name, size) in new[] { ("ftyp", 8), ("moov", 8), ("mdat", payload) })
        {
            var header = new byte[8]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)(size + 8)); Encoding.ASCII.GetBytes(name).CopyTo(header,4);
            file.Write(header); file.Write(new byte[size]);
        }
        return path;
    }
    [Fact]
    public async Task MigrationPreservesLegacyIdentityHistoryAndRenameAcrossDiscoveryAndRedownload()
    {
        await InitializeAsync(); var file = Video("legacy.mp4"); var catalog = new SqliteContentCatalog(Paths);
        var item = new ContentItem("legacy", ContentSource.Missions,"Título original",new(2026,10,3),new("https://example.test/item"),[],SyncState.Ready,true,file);
        await catalog.UpsertAsync([item]); await catalog.RecordPlaybackAsync(item.Id,DateTimeOffset.UtcNow);
        await new SinaloDatabase(Paths).InitializeAsync(); await new SinaloDatabase(Paths).InitializeAsync();
        var media = Assert.Single((await Library.QueryAsync(new())).Items); Assert.Equal("legacy",media.Id); Assert.Equal(1,media.PlayCount); Assert.True(media.IsPinned);
        await Library.RenameAsync(media.Id,"Nome escolhido",null); await catalog.UpsertAsync([item with { Title="Nova descoberta" }]);
        Assert.Equal("Nome escolhido",(await Library.FindAsync(media.Id))!.Name); Assert.Equal("Nome escolhido",(await catalog.FindByIdAsync(item.Id))!.Title);
        Assert.Equal(1,(await Library.QueryAsync(new(Search:"03/10/2026",Origin:"0"))).Total);
        await catalog.DeleteAsync(item.Id); Assert.Empty((await Library.QueryAsync(new())).Items);
        await catalog.UpsertAsync([item]); Assert.Equal("Nome escolhido",(await Library.FindAsync(media.Id))!.Name);
        Assert.Equal(file,(await Library.FindAsync(media.Id))!.LocalPath);
    }
    [Fact]
    public async Task CopyReferenceDuplicatesAndSameNamesAreExplicitAndSafe()
    {
        await InitializeAsync(); var first=Video("a/same.mp4"); var second=Video("b/same.mp4");
        var result=await Importer().ImportAsync(new([first,second,first])); Assert.Equal(2,result.Imported); Assert.Empty(result.Failures);
        var page=await Library.QueryAsync(new(Origin:"Importados")); Assert.Equal(2,page.Total); Assert.All(page.Items,m=>Assert.True(m.IsImported));
        Assert.All(page.Items,m=>Assert.StartsWith(Path.Combine(Paths.GetContentPath(),"imported"),m.LocalPath!));
        Assert.All(page.Items,m=>Assert.Equal(64,m.Sha256!.Length)); Assert.True(File.Exists(first));
        Assert.Equal(2,(await Importer().ImportAsync(new([first,second]))).Existing);
        var noNewCopies = await Importer(_ => 0).ImportAsync(new([first,second]));
        Assert.Equal(2, noNewCopies.Existing); Assert.Empty(noNewCopies.Failures);
        Assert.Equal(1,(await Importer().ImportAsync(new([page.Items[0].LocalPath!]))).Existing);
        var reference=Video("reference.mp4"); Assert.Equal(1,(await Importer().ImportAsync(new([reference],MediaStorageMode.Referenced))).Imported);
        var item=await Library.FindByPathAsync(reference); Assert.Equal(reference,item!.LocalPath); Assert.Equal(MediaStorageMode.Referenced,item.StorageMode);
        Assert.Equal(3,(await Library.QueryAsync(new(Sort:LibrarySort.AddedAt))).Total);
        await Library.RenameAsync(item.Id,"Um nome novo",new(2026,10,10)); Assert.Equal(1,(await Library.QueryAsync(new(Search:"10/10/2026"))).Total);
        Assert.Equal(reference,(await Library.FindAsync(item.Id))!.LocalPath);
    }
    [Fact]
    public async Task FolderImportRecursionUnsupportedFilesAndInvalidVideosHaveSeparateResults()
    {
        await InitializeAsync(); Video("folder/ok.mp4"); Video("folder/sub/child.mp4");
        await File.WriteAllTextAsync(Path.Combine(Root,"originals","folder","bad.mp4"),"not mp4");
        await File.WriteAllTextAsync(Path.Combine(Root,"originals","folder","song.mp3"),"audio");
        var top=await Importer().ImportAsync(new([Path.Combine(Root,"originals","folder")],IncludeSubfolders:false));
        Assert.Equal(1,top.Imported); Assert.Equal(1,top.Ignored); Assert.Single(top.Failures);
        var recursive=await Importer().ImportAsync(new([Path.Combine(Root,"originals","folder")])); Assert.Equal(1,recursive.Imported); Assert.Equal(1,recursive.Existing);
        var invalid=await Importer().ImportAsync(new([Path.Combine(Root,"does-not-exist")])); Assert.NotEmpty(invalid.Failures);
    }
    [Fact]
    public async Task LowSpaceCancellationAndPlaybackDoNotPublishPartialFiles()
    {
        await InitializeAsync(); var file=Video("large.mp4",256*1024);
        var low=await Importer(_=>0).ImportAsync(new([file])); Assert.Equal(0,low.Imported); Assert.NotEmpty(low.Failures);
        Assert.Equal(1,(await Importer(_=>0).ImportAsync(new([file],MediaStorageMode.Referenced))).Imported);
        var other=Video("other.mp4",256*1024); using var cancellation=new CancellationTokenSource();
        _activity.SetActive(true);
        var waiting=Importer().ImportAsync(new([other]),token:cancellation.Token);
        await Task.Delay(30); Assert.False(waiting.IsCompleted); cancellation.Cancel();
        Assert.True((await waiting).Cancelled); _activity.SetActive(false);
        Assert.Equal(1,(await Library.QueryAsync(new())).Total);
        Assert.Empty(Directory.EnumerateFiles(Paths.GetContentPath(),"*.part",SearchOption.AllDirectories));
        using var midway=new CancellationTokenSource();
        var progress=new ImmediateProgress(update=> { if(update.Stage=="Copiando e conferindo" && update.Percentage>0) midway.Cancel(); });
        var cancelled=await Importer().ImportAsync(new([other]),progress,midway.Token); Assert.True(cancelled.Cancelled);
        Assert.Equal(1,(await Library.QueryAsync(new())).Total);
        Assert.Empty(Directory.EnumerateFiles(Paths.GetContentPath(),"*.part",SearchOption.AllDirectories));
    }
    [Fact]
    public async Task MissingReferenceCanBeLocatedButIsNeverDeleted()
    {
        await InitializeAsync(); var original=Video("ref.mp4"); await Importer().ImportAsync(new([original],MediaStorageMode.Referenced));
        var media=(await Library.FindByPathAsync(original))!; File.Delete(original);
        Assert.Equal(MediaAvailability.Missing,(await Library.FindAsync(media.Id))!.Availability);
        var replacement=Video("replacement.mp4"); await Files.LocateAsync(media.Id,replacement);
        Assert.Equal(replacement,(await Library.FindAsync(media.Id))!.LocalPath);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Files.RemoveAsync(media.Id,true));
        _activity.SetActive(true); await Assert.ThrowsAsync<InvalidOperationException>(()=>Files.RemoveAsync(media.Id,false)); _activity.SetActive(false);
        await Files.RemoveAsync(media.Id,false); Assert.True(File.Exists(replacement)); Assert.Null(await Library.FindAsync(media.Id));
    }
    [Fact]
    public async Task ManagedRemovalRequiresOwnershipAndProtectsSharedFiles()
    {
        await InitializeAsync(); var original=Video("copy.mp4"); await Importer().ImportAsync(new([original]));
        var media=Assert.Single((await Library.QueryAsync(new())).Items);
        await Library.SaveImportedAsync(new("ref", "Shared", null,null,null,media.LocalPath,"other",MediaStorageMode.Referenced,MediaAvailability.Available,DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Files.RemoveAsync(media.Id,true));
        await Files.RemoveAsync("ref",false); await Files.RemoveAsync(media.Id,true); Assert.False(File.Exists(media.LocalPath)); Assert.True(File.Exists(original));
        await Importer().ImportAsync(new([original])); media=Assert.Single((await Library.QueryAsync(new())).Items);
        await Files.RemoveAsync(media.Id,false); Assert.True(File.Exists(media.LocalPath));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Files.RemoveAsync("missing",false));
    }
    [Fact]
    public async Task ManagedFilesMoveButReferencesInsideTheContentRootRemainUntouched()
    {
        await InitializeAsync(); var original=Video("move.mp4"); await Importer().ImportAsync(new([original]));
        var reference=Path.Combine(Paths.GetContentPath(),"external-ref.mp4"); File.Copy(original,reference);
        await Importer().ImportAsync(new([reference],MediaStorageMode.Referenced));
        var oldItems=(await Library.QueryAsync(new())).Items; var target=Path.Combine(Root,"new-content");
        await new LocalContentPathMigrationService(Paths,new SqliteContentCatalog(Paths),Library,_operations,_activity).MoveAsync(target);
        var moved=(await Library.QueryAsync(new())).Items; var managed=moved.Single(x=>x.StorageMode==MediaStorageMode.Managed);
        Assert.StartsWith(target,managed.LocalPath!); Assert.True(File.Exists(managed.LocalPath));
        Assert.False(File.Exists(oldItems.Single(x=>x.StorageMode==MediaStorageMode.Managed).LocalPath));
        Assert.Equal(reference,moved.Single(x=>x.StorageMode==MediaStorageMode.Referenced).LocalPath); Assert.True(File.Exists(reference));
    }
    [Fact]
    public async Task MigrationRejectsSameSizedDifferentDestinationAndActivePlayback()
    {
        await InitializeAsync(); var source=Video("conflict.mp4"); await Importer().ImportAsync(new([source])); var media=Assert.Single((await Library.QueryAsync(new())).Items);
        var target=Path.Combine(Root,"new-content"); var destination=Path.Combine(target,Path.GetRelativePath(Paths.GetContentPath(),media.LocalPath!));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!); var bytes=await File.ReadAllBytesAsync(media.LocalPath!); bytes[^1]=1; await File.WriteAllBytesAsync(destination,bytes);
        var migration=new LocalContentPathMigrationService(Paths,new SqliteContentCatalog(Paths),Library,_operations,_activity);
        await Assert.ThrowsAsync<IOException>(()=>migration.MoveAsync(target)); Assert.True(File.Exists(media.LocalPath));
        _activity.SetActive(true); await Assert.ThrowsAsync<InvalidOperationException>(()=>migration.MoveAsync(target)); _activity.SetActive(false);
    }
    [Fact]
    public async Task RecoveryRemovesOnlyJournaledOrphansAndRetainsCommittedCopies()
    {
        await InitializeAsync(); var original=Video("committed.mp4"); await Importer().ImportAsync(new([original])); var media=Assert.Single((await Library.QueryAsync(new())).Items);
        foreach(var id in new[] { media.Id,"local-"+Guid.NewGuid().ToString("N") })
        {
            var job=Path.Combine(Paths.GetContentPath(),".sinalo-imports",id[6..]); Directory.CreateDirectory(job);
            await File.WriteAllTextAsync(Path.Combine(job,"job.json"),System.Text.Json.JsonSerializer.Serialize(new { Id=id,FileName="committed.mp4" }));
            var destination=Path.Combine(Paths.GetContentPath(),"imported",id,"committed.mp4"); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if(!File.Exists(destination)) File.Copy(original,destination);
        }
        await Importer().RecoverAsync(); Assert.True(File.Exists(media.LocalPath)); Assert.Single(Directory.EnumerateFiles(Path.Combine(Paths.GetContentPath(),"imported"),"*.mp4",SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(Paths.GetContentPath(),".sinalo-imports")));
    }
    [Fact]
    public async Task PaginationNullDatesAndEscapedSearchAreConsistent()
    {
        await InitializeAsync(); var file=Video("small.mp4");
        for(var i=0;i<105;i++) await Library.SaveImportedAsync(new("item"+i,$"Item {i:000}",null,null,i%2==0 ? new(2026,10,3) : null,file,"origin"+i,MediaStorageMode.Referenced,MediaAvailability.Available,DateTimeOffset.UtcNow));
        var page=await Library.QueryAsync(new(Page:2)); Assert.Equal(105,page.Total); Assert.Equal(5,page.Items.Count);
        var dates=await Library.QueryAsync(new(Sort:LibrarySort.UsageDate,PageSize:100)); Assert.NotNull(dates.Items[0].UsageDate); Assert.Null(dates.Items[^1].UsageDate);
        Assert.Equal(0,(await Library.QueryAsync(new(Search:"%"))).Total);
        await Assert.ThrowsAsync<ArgumentException>(()=>Library.RenameAsync("item0"," ",null));
        Assert.Null(await Library.FindAsync("missing"));
    }
    [Theory]
    [InlineData("empty.mp4",0)] [InlineData("tiny.mp4",4)] [InlineData("wrong.mp3",32)]
    public async Task ValidatorRejectsInvalidExtensionsAndIncompleteStructures(string name,int bytes)
    {
        Directory.CreateDirectory(Root); var path=Path.Combine(Root,name); await File.WriteAllBytesAsync(path,new byte[bytes]);
        await Assert.ThrowsAsync<InvalidDataException>(()=>new Mp4LibraryValidator().ValidateAsync(path));
        Assert.Null((await new Mp4LibraryValidator().ReadAsync(path)).DurationSeconds);
    }
    internal sealed class ImmediateProgress(Action<LibraryImportProgress> report) : IProgress<LibraryImportProgress> { public void Report(LibraryImportProgress update)=>report(update); }
    public void Dispose() { SqliteConnection.ClearAllPools(); if(Directory.Exists(Root)) Directory.Delete(Root,true); }
}
