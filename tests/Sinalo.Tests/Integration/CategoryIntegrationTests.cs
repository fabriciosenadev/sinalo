using System.IO;
using Microsoft.Data.Sqlite;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.App.ViewModels;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class CategoryIntegrationTests
{
    private sealed class Fixture:IDisposable
    {
        public LibraryIntegrationTests Files {get;}=new();
        public LocalSinaloPathService Paths {get;}
        public SqliteLibraryRepository Library {get;}
        public PlaybackActivityGate Activity {get;}=new();
        public ContentOperationGate Gate {get;}=new();
        public SqliteMediaCategoryService Categories {get;}
        public bool Busy {get;set;}
        public Fixture(){Paths=new(rootPath:Files.Root);Library=new(Paths);Categories=new(Paths,Library,Gate,Activity,()=>Busy);}
        public Task Init()=>Files.InitializeAsync();
        public async Task<LibraryMedia> Import(string name,string cat,MediaStorageMode mode=MediaStorageMode.Managed)
        {
            var original=Files.Video(name);var validator=new Mp4LibraryValidator(Activity);
            var result=await new LocalLibraryImportService(Paths,Library,validator,validator,Gate,Activity).ImportAsync(new([original],mode,CategoryId:cat));
            Assert.Empty(result.Failures);return (await Library.FindByPathAsync(original))!;
        }
        public void Dispose()=>Files.Dispose();
    }
    [Theory]
    [InlineData("")][InlineData("\n")][InlineData("A\tB")]
    public void InvalidNamesRejected(string name)=>Assert.Throws<ArgumentException>(()=>MediaCategory.ValidateName(name));
    [Fact] public void StableSourceMappingAndNameNormalization()
    {
        foreach(var source in Enum.GetValues<ContentSource>())Assert.Equal(source,MediaCategory.SourceFor(MediaCategory.ForSource(source)));
        Assert.Null(MediaCategory.SourceFor("custom"));Assert.Equal("Louvores",MediaCategory.ValidateName(" Louvores "));
        Assert.Equal(MediaCategory.NameKey("Saúde"),MediaCategory.NameKey("Sau\u0301de"));
        Assert.Throws<ArgumentException>(()=>MediaCategory.ValidateName(new('x',81)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MediaCategory.ForSource((ContentSource)99));
    }
    [Fact] public async Task NamesOrderingSystemProtectionAndRevision()
    {
        using var f=new Fixture();await f.Init();Assert.Equal(4,(await f.Categories.ListAsync()).Count);
        var a=await f.Categories.CreateAsync(" Louvores ");var b=await f.Categories.CreateAsync("Jovens");
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Categories.CreateAsync("louvores"));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Categories.CreateAsync("Minuto de Saúde"));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Categories.RenameAsync(b,"Louvores"));
        await f.Categories.ReorderAsync(b.Id,-1);Assert.Equal(b.Id,(await f.Categories.ListAsync()).First(x=>!x.IsSystem).Id);
        await f.Categories.ReorderAsync(a.Id,1);await f.Categories.ReorderAsync("absent",1);
        a=(await f.Categories.ListAsync()).Single(x=>x.Id==a.Id);
        await f.Categories.RenameAsync(a,"Música");await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RenameAsync(a,"Outro"));
        var system=(await f.Categories.ListAsync()).First(x=>x.IsSystem);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RenameAsync(system,"Sistema"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(system,null));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(system with{IsSystem=false},null));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.ValidateDestinationAsync("absent"));
        await new SinaloDatabase(f.Paths).InitializeAsync();Assert.Equal(6,(await f.Categories.ListAsync()).Count);
    }
    [Fact] public async Task TransferPreservesCopiesReferencesMissingFilesAndHistory()
    {
        using var f=new Fixture();await f.Init();var a=await f.Categories.CreateAsync("Culto");var b=await f.Categories.CreateAsync("Jovens");
        var copy=await f.Import("copy.mp4",a.Id);var reference=await f.Import("ref.mp4",a.Id,MediaStorageMode.Referenced);
        await f.Library.RecordPlaybackAsync(copy.Id);File.Delete(reference.LocalPath!);
        var before=await f.Library.FindAsync(copy.Id);await f.Categories.AssignAsync([copy.Id,copy.Id],b.Id);
        Assert.Equal(b.Id,(await f.Library.FindAsync(copy.Id))!.CategoryId);
        await f.Categories.AssignAsync([copy.Id],a.Id);
        await f.Categories.RemoveAsync(a,"missions");
        var after=await f.Library.FindAsync(copy.Id);Assert.Equal(before!.LocalPath,after!.LocalPath);Assert.Equal(before.PlayCount,after.PlayCount);
        Assert.Equal(before.Sha256,after.Sha256);Assert.Null(after.Source);Assert.Equal("missions",after.CategoryId);
        Assert.Equal("missions",(await f.Library.FindAsync(reference.Id))!.CategoryId);
        Assert.DoesNotContain(await f.Categories.ListAsync(),x=>x.Id==a.Id);
        Assert.Equal(2,(await f.Library.QueryAsync(new(CategoryId:"missions"))).Total);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.AssignAsync(["absent"],b.Id));
    }
    [Fact] public async Task DefinitiveDeleteNeverDeletesReferencedOriginalEvenInsideContent()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Vídeos");
        var copy=await f.Import("copy.mp4",cat.Id);var reference=await f.Import("ref.mp4",cat.Id,MediaStorageMode.Referenced);
        var external=reference.LocalPath!;var originalBytes=await File.ReadAllBytesAsync(external);
        var impact=await f.Categories.InspectAsync(cat.Id);Assert.Equal(1,impact.ManagedCopies);Assert.Equal(1,impact.References);
        var removed=new List<string>();f.Categories.MediaRemoved+=ids=>removed.AddRange(ids);
        await f.Categories.RemoveAsync(cat,null);Assert.False(File.Exists(copy.LocalPath));Assert.True(File.Exists(copy.OriginalPath));
        Assert.Equal(originalBytes,await File.ReadAllBytesAsync(external));Assert.Null(await f.Library.FindAsync(reference.Id));
        Assert.Empty((await f.Library.QueryAsync(new())).Items);Assert.Equal(2,removed.Count);
    }
    [Fact] public async Task ExplicitRetryRecoversInterruptedDeleteWithoutTouchingReplacement()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Excluir");var media=await f.Import("locked.mp4",cat.Id);
        using(var locked=new FileStream(media.LocalPath!,FileMode.Open,FileAccess.Read,FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(()=>f.Categories.RemoveAsync(cat,null));
        var pending=(await f.Categories.ListAsync()).Single(x=>x.Id==cat.Id);Assert.True(pending.IsDeleting);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.ValidateDestinationAsync(cat.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.AssignAsync([media.Id],"missions"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(pending,"missions"));
        File.Delete(media.LocalPath!);await File.WriteAllTextAsync(media.LocalPath!,"replacement");
        await Assert.ThrowsAnyAsync<IOException>(()=>f.Categories.RemoveAsync(pending,null));Assert.True(File.Exists(media.LocalPath));
        File.Delete(media.LocalPath!);await f.Categories.RemoveAsync(pending,null);Assert.Null(await f.Library.FindAsync(media.Id));
    }
    [Fact] public async Task BusyPlaybackInvalidTransferAndExternalManagedFileBlockDeletion()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Privado");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(cat,cat.Id));
        f.Busy=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(cat,null));f.Busy=false;
        f.Activity.SetActive(true);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(cat,null));f.Activity.SetActive(false);
        var file=f.Files.Video("outside.mp4");await f.Library.SaveImportedAsync(new("local-"+Guid.NewGuid().ToString("N"),"Original",null,null,null,file,null,MediaStorageMode.Managed,MediaAvailability.Available,DateTimeOffset.UtcNow,CategoryId:cat.Id));
        await Assert.ThrowsAnyAsync<IOException>(()=>f.Categories.RemoveAsync(cat,null));Assert.True(File.Exists(file));
    }
    [Fact] public async Task LegacySourceOriginAndManualAssignmentSurviveRefresh()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Culto");var catalog=new SqliteContentCatalog(f.Paths);
        var file=f.Files.Video("legacy.mp4");var item=new ContentItem("missions-youtube-abcdefghijk",ContentSource.Missions,"Original",new(2026,10,10),new("https://example.test"),[],SyncState.Ready,true,file);
        await catalog.UpsertAsync([item]);Assert.Equal("missions",(await f.Library.FindAsync(item.Id))!.CategoryId);
        await f.Categories.AssignAsync([item.Id],cat.Id);await catalog.UpsertAsync([item with{Title="Atualizado"}]);
        await new SinaloDatabase(f.Paths).InitializeAsync();var media=await f.Library.FindAsync(item.Id);
        Assert.Equal(cat.Id,media!.CategoryId);Assert.Equal(ContentSource.Missions,media.Source);Assert.True(media.IsPinned);
        Assert.Equal(item.Id,(await f.Categories.FindLinkedAsync("abcdefghijk",cat.Id))!.Id);
        Assert.Null(await f.Categories.FindLinkedAsync("abcdefghijk","health"));
    }
    [Fact] public async Task CustomLinkedPublicationAndMovedDeduplication()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Link");var other=await f.Categories.CreateAsync("Destino");
        var format=new LinkedVideoFormat("18",null,360,100);var video=new LinkedVideo("abcdefghijk","Título",new("https://youtu.be/abcdefghijk"),new(2026,10,10),[format]);
        var request=new LinkedVideoDownloadRequest(video,format,ContentSource.Health,video.PublishedDate,cat.Id,cat.Name);
        var file=Path.Combine(f.Paths.GetContentPath(),"linked.mp4");File.Copy(f.Files.Video("download.mp4"),file);
        var downloaded=new ContentItem(request.ItemId,request.Destination,video.Title,video.PublishedDate,video.PageUri,[new("asset",video.PageUri,"linked.mp4",new FileInfo(file).Length,"hash")],SyncState.Ready,true,file);
        await f.Categories.PublishLinkedAsync(request,downloaded);var media=await f.Categories.FindLinkedAsync(video.Id,cat.Id);
        Assert.Null(media!.Source);Assert.Null(media.UsageDate);Assert.True(media.CanPlay);
        Assert.Equal("Vídeo por link",media.OriginLabel);
        await f.Categories.AssignAsync([media.Id],other.Id);Assert.Equal(media.Id,(await f.Categories.FindLinkedAsync(video.Id,other.Id))!.Id);
        await f.Categories.RegisterLinkedAsync(media.Id,video.Id);await f.Categories.RegisterLinkedAsync(media.Id,video.Id);
        Assert.Null(await f.Categories.FindLinkedAsync(video.Id,cat.Id));
    }
    [Fact] public async Task ViewModelManagementAndInvalidNameFailure()
    {
        using var f=new Fixture();await f.Init();var model=new CategoryManagerViewModel(f.Categories);
        await model.RefreshAsync();model.Name="Louvor";await model.CreateCommand.ExecuteAsync(null);Assert.Single(model.Items);
        model.Selected=model.Items[0];model.Name="Música";await model.RenameCommand.ExecuteAsync(null);Assert.Equal("Música",model.Items[0].Name);
        await model.MoveUpCommand.ExecuteAsync(null);await model.MoveDownCommand.ExecuteAsync(null);
        model.Name="";await model.CreateCommand.ExecuteAsync(null);Assert.Contains("80",model.Message);
        model.Selected=model.Items[0];await model.RemoveAsync("missions");Assert.Empty(model.Items);Assert.False(model.Busy);
        await model.RenameCommand.ExecuteAsync(null);Assert.Contains("Selecione",model.Message);
    }
    [Fact] public async Task OperatorImportsMovesExistingSearchesPlaysAndRemovesThroughViewModel()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Culto");
        var catalog=new SqliteContentCatalog(f.Paths);var validator=new Mp4LibraryValidator(f.Activity);
        var importer=new LocalLibraryImportService(f.Paths,f.Library,validator,validator,f.Gate,f.Activity);
        var fileService=new LocalLibraryFileService(f.Library,f.Paths,validator,f.Gate,f.Activity);
        var player=new LibraryPlaybackService(f.Library,catalog,new Launcher(),validator);
        var model=new LibraryViewModel(f.Library,importer,fileService,player,catalog,new LocalContentDeletionService(catalog,f.Paths),categories:f.Categories);
        await model.LoadCategoriesAsync();Assert.Equal(MediaCategory.Uncategorized,model.ImportCategory!.Id);Assert.Equal("Biblioteca de vídeos",model.LibraryTitle);
        model.ImportCategory=model.CategoryOptions.Single(x=>x.Id==cat.Id);model.CategoryFilter=model.CategoryFilters.Single(x=>x.Id==cat.Id);
        Assert.Equal("Culto",model.LibraryTitle);var original=f.Files.Video("workflow.mp4");await model.ImportAsync([original]);
        Assert.Single(model.Items);Assert.True(model.IsFiltered);var id=model.Items[0].Id;
        await model.MoveSelectedAsync([id]);Assert.Contains("Escolha",model.Message);
        model.MoveCategory=model.CategoryOptions.Single(x=>x.Id=="health");await model.MoveSelectedAsync([id]);Assert.Empty(model.Items);
        await model.ImportAsync([original]);Assert.Single(model.ExistingImportIds);await model.MoveExistingImportsAsync();Assert.Single(model.Items);
        model.CategoryFilter=model.CategoryFilters[0];await model.RefreshAsync();Assert.Single(await model.AllMediaAsync());
        await model.LoadCategoriesAsync();Assert.Equal(cat.Id,model.ImportCategory.Id);
        Assert.True((await model.PlayMediaAsync(id,new(1))).Started);Assert.Equal(1,(await f.Library.FindAsync(id))!.PlayCount);
        await model.RemoveMediaAsync("absent");await model.RemoveMediaAsync(id);Assert.True(File.Exists(original));Assert.Empty(await model.AllMediaAsync());
        await model.MoveExistingImportsAsync();Assert.Contains("removida",model.Message);
        await model.ClearFiltersCommand.ExecuteAsync(null);Assert.False(model.IsFiltered);
        model.CategoryFilter=new("pending","Pendente",IsDeleting:true);Assert.False(model.CanImport);
        await model.ImportAsync([original]);Assert.Contains("exclusão",model.ImportMessage);
        var noCategories=new LibraryViewModel(f.Library,importer,fileService,player,catalog,new LocalContentDeletionService(catalog,f.Paths));
        await noCategories.LoadCategoriesAsync();await noCategories.MoveExistingImportsAsync();await noCategories.MoveSelectedAsync([]);
    }
    [Theory]
    [InlineData("")][InlineData("14/10/2026")]
    public async Task HomeCustomDestinationAcceptsOptionalDateAndPreservesOnReload(string date)
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Jovens");
        var home=new HomeViewModel(new SaturdayWindowService(),f.Paths,[]);var format=new LinkedVideoFormat("18",null,360,100);
        var video=new LinkedVideo("abcdefghijk","Vídeo",new("https://youtu.be/abcdefghijk"),new(2026,10,10),[format]);
        home.LinkedVideoDestinations.Add(new(ContentSource.Health,cat.Name,cat.Id));home.SelectedLinkedVideoDestination=home.LinkedVideoDestinations.Last();
        home.SetInspectedLinkedVideo(video);home.LinkedVideoDateText=date;Assert.True(home.CanQueueLinkedVideo);
        var request=home.CreateLinkedVideoRequest();Assert.True(request.IsCustomDestination);Assert.Equal(cat.Id,request.EffectiveCategoryId);
        Assert.Equal(string.IsNullOrEmpty(date),request.CustomUsageDate is null);
        var replacement=new HomeViewModel(new SaturdayWindowService(),f.Paths,[]);replacement.RestoreLinkedVideoState(home);Assert.Equal(cat.Id,replacement.SelectedLinkedVideoDestination!.CategoryId);
        home.LinkedVideoDateText="inválida";Assert.False(home.CanQueueLinkedVideo);Assert.Throws<InvalidOperationException>(()=>home.CreateLinkedVideoRequest());
        home.SelectedLinkedVideoDestination=home.LinkedVideoDestinations[0];home.LinkedVideoDateText="";Assert.False(home.CanQueueLinkedVideo);
        Assert.Equal("missions",new LinkedVideoDownloadRequest(video,format,ContentSource.Missions,video.PublishedDate).EffectiveCategoryId);
    }
    [Fact] public async Task ProgramListsUseCategoryNotSourceAndPreserveOriginMetadata()
    {
        using var f=new Fixture();await f.Init();var item=await f.Import("program.mp4","health");var missing=await f.Import("missing.mp4","missions",MediaStorageMode.Referenced);
        File.Delete(missing.LocalPath!);var home=new HomeViewModel(new SaturdayWindowService(),f.Paths,[]);
        home.SetCategorizedProgramItems((await f.Library.QueryAsync(new())).Items);home.SelectProgram("Minuto de Saúde");
        Assert.Equal(item.Id,Assert.Single(home.CatalogItems).Id);Assert.Null(item.Source);
        await f.Categories.AssignAsync([item.Id],MediaCategory.Uncategorized);
        home.SetCategorizedProgramItems((await f.Library.QueryAsync(new())).Items);Assert.Empty(home.CatalogItems);
    }
    [Fact] public async Task PlaybackAndPinNotificationsKeepManualProgramCategory()
    {
        using var f=new Fixture();await f.Init();var catalog=new SqliteContentCatalog(f.Paths);var file=f.Files.Video("played.mp4");
        var item=new ContentItem("original",ContentSource.Health,"Vídeo",new(2026,10,10),new("https://example.test"),[],SyncState.Ready,true,file);
        await catalog.UpsertAsync([item]);await f.Categories.AssignAsync([item.Id],"missions");
        var home=new HomeViewModel(new SaturdayWindowService(),f.Paths,[]);home.SetCategorizedProgramItems((await f.Library.QueryAsync(new())).Items);home.SelectProgram("Informativo das Missões");
        home.MarkItemAsReady(item);home.MarkItemAsPlayed(item with{PlayCount=1});home.MarkItemPinned(item with{IsPinned=false});
        Assert.Equal("Informativo das Missões",Assert.Single(home.CatalogItems).SourceName);
    }
    private sealed class Launcher:IPlaybackLauncher
    {
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath,PlaybackLaunchOptions options,CancellationToken cancellationToken=default)=>Task.FromResult(new PlaybackLaunchResult(true,"MPV","Aberto"));
    }
    [Fact] public async Task StaleConfirmationDoesNotDeleteNewMedia()
    {
        using var f=new Fixture();await f.Init();var cat=await f.Categories.CreateAsync("Confirmar");var preview=await f.Categories.InspectAsync(cat.Id);
        var media=await f.Import("new-after-preview.mp4",cat.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Categories.RemoveAsync(cat,null,expectedMediaIds:preview.MediaIds));Assert.True(File.Exists(media.LocalPath));
        var final=await f.Categories.InspectAsync(cat.Id);await f.Categories.RemoveAsync(cat,"missions",expectedMediaIds:final.MediaIds);Assert.Equal("missions",(await f.Library.FindAsync(media.Id))!.CategoryId);
    }
    [Fact] public async Task CustomLinkDownloadsAndPublishesThroughExistingRealFilePipeline()
    {
        using var f=new Fixture();await f.Init();var category=await f.Categories.CreateAsync("Vídeo por link");
        var format=new LinkedVideoFormat("18",null,360,2048);var video=new LinkedVideo("abcdefghijk","Real MP4",new("https://youtu.be/abcdefghijk"),new(2026,10,10),[format]);
        var request=new LinkedVideoDownloadRequest(video,format,ContentSource.Health,video.PublishedDate,category.Id,category.Name);
        var original=f.Files.Video("fixture.mp4",2048);
        var downloader=new LinkedVideoService(f.Paths,toolDirectory:f.Files.Root,toolRunner:(args,_,token)=>
        {
            var template=args[args.ToList().IndexOf("--output")+1];File.Copy(original,template.Replace("%(id)s",video.Id).Replace("%(ext)s","mp4"));
            return Task.FromResult(new VideoToolResult(0,"",""));
        },freeSpace:_=>long.MaxValue);
        var ready=await downloader.DownloadAsync(request);await f.Categories.PublishLinkedAsync(request,ready);
        var media=await f.Categories.FindLinkedAsync(video.Id,category.Id);Assert.NotNull(media);Assert.True(media!.IsPinned);
        Assert.Contains(Path.Combine("categories",category.Id),media.LocalPath);Assert.Null(await new SqliteContentCatalog(f.Paths).FindByIdAsync(ready.Id));
        await Assert.ThrowsAsync<InvalidDataException>(()=>downloader.DownloadAsync(request with{CategoryId="../../outside"}));
        await Assert.ThrowsAsync<InvalidDataException>(()=>downloader.DownloadAsync(request with{ExistingMediaId="../outside"}));
        File.Delete(media.LocalPath!);
        var again=request with {CategoryId="missions",LibraryStorage=true,ExistingMediaId=media.Id};
        var repaired=await downloader.DownloadAsync(again);Assert.Contains(Path.Combine("categories",media.Id),repaired.LocalPath);
        await f.Categories.PublishLinkedAsync(again,repaired);Assert.Equal(media.Id,(await f.Categories.FindLinkedAsync(video.Id,"missions"))!.Id);
    }
}
