using System.Diagnostics;
using System.IO;
using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using Sinalo.Tests.Integration;

namespace Sinalo.Tests.EndToEnd;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class LibraryWorkflowTests
{
    [Fact]
    public async Task OperatorImportsEditsPlaysSearchesAndRemovesWithoutDeletingOriginal()
    {
        using var fixture=new LibraryIntegrationTests(); await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root); var library=new SqliteLibraryRepository(paths); var catalog=new SqliteContentCatalog(paths);
        var activity=new PlaybackActivityGate(); var operations=new ContentOperationGate(); var validator=new Mp4LibraryValidator(activity);
        var importer=new LocalLibraryImportService(paths,library,validator,validator,operations,activity);
        var files=new LocalLibraryFileService(library,paths,validator,operations,activity); var launcher=new Launcher();
        var presentation=false;
        var model=new LibraryViewModel(library,importer,files,new(library,catalog,launcher,validator),catalog,new LocalContentDeletionService(catalog,paths),
            ()=>Task.FromResult<PlaybackLaunchOptions?>(new(2)),()=>presentation);
        var original=fixture.Video("local.mp4");
        await model.ImportAsync([original]); Assert.False(model.IsImporting); Assert.Contains("1 importado",model.Message); model.Selected=Assert.Single(model.Items);
        await model.WhenImportIdleAsync();
        Assert.True(model.IsImported); Assert.True(model.CanDeleteCopy); Assert.True(model.CanEditDate); Assert.Contains("Cópia",model.SelectedDetails);
        model.EditName="Nome do operador"; model.EditDate="31/99/2026"; await model.SaveNameCommand.ExecuteAsync(null); Assert.Contains("DD/MM",model.Message);
        model.EditDate="10/10/2026"; await model.SaveNameCommand.ExecuteAsync(null); Assert.Equal("Nome do operador",model.Selected!.Name);
        await model.PlayCommand.ExecuteAsync(null); Assert.Equal(2,launcher.Output!.FullscreenScreenNumber); Assert.Equal(1,model.Selected!.PlayCount);
        Assert.NotNull(model.Selected.FirstPlayedAtUtc); Assert.NotNull(model.Selected.LastPlayedAtUtc);
        presentation=true; await model.PlayCommand.ExecuteAsync(null); Assert.Contains("Feche",model.Message); Assert.Equal(1,launcher.Launches); presentation=false;
        model.Search="10/10/2026"; await model.SearchNowCommand.ExecuteAsync(null); Assert.Single(model.Items);
        await model.NextPageCommand.ExecuteAsync(null); await model.PreviousPageCommand.ExecuteAsync(null); Assert.Contains("Página 1",model.PageLabel);
        model.Selected=model.Items[0]; await model.TogglePinCommand.ExecuteAsync(null); model.DeleteCopy=true;
        await model.RemoveCommand.ExecuteAsync(null); Assert.Empty(model.Items); Assert.True(File.Exists(original));
        Assert.False(model.HasSelection); Assert.Equal("Selecione um vídeo",model.SelectedDetails);
        model.CancelImport();
    }
    [Fact]
    public async Task ARealMp4CanBeImportedAndLaunchedOfflineThroughExistingMpv()
    {
        using var fixture=new LibraryIntegrationTests(); await fixture.InitializeAsync();
        var path=Path.Combine(fixture.Root,"real.mp4");
        var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"binaries","video-download","ffmpeg.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        foreach(var value in new[]{"-hide_banner","-loglevel","error","-f","lavfi","-i","color=c=black:s=64x64:r=10","-t","3","-c:v","mpeg4","-y",path}) start.ArgumentList.Add(value);
        using(var process=Process.Start(start)!) { var error=process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); Assert.True(process.ExitCode==0,await error); }
        var paths=new LocalSinaloPathService(rootPath:fixture.Root); var library=new SqliteLibraryRepository(paths); var activity=new PlaybackActivityGate(); var validator=new Mp4LibraryValidator(activity);
        Assert.Equal(1,(await new LocalLibraryImportService(paths,library,validator,validator,new(),activity).ImportAsync(new([path]))).Imported);
        var media=Assert.Single((await library.QueryAsync(new())).Items);
        await using var player=new MpvPlaybackLauncher();
        var service=new LibraryPlaybackService(library,new SqliteContentCatalog(paths),player,validator);
        Assert.True((await service.PlayAsync(media.Id,new(1))).Started);
        Assert.Equal(1,(await library.FindAsync(media.Id))!.PlayCount);
    }
    [Fact]
    public async Task SyncedLibraryPlaybackUsesTheOriginalHistoryAndCleanupPreservesImports()
    {
        using var fixture=new LibraryIntegrationTests(); await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root); var catalog=new SqliteContentCatalog(paths); var library=new SqliteLibraryRepository(paths);
        var file=fixture.Video("synced.mp4"); var managed=Path.Combine(paths.GetContentPath(),"synced.mp4"); File.Copy(file,managed);
        var item=new ContentItem("synced",ContentSource.Health,"Vídeo do programa",new(2020,1,1),new("https://example.test"),[],SyncState.Ready,false,managed);
        await catalog.UpsertAsync([item]); var validator=new Mp4LibraryValidator(); var launcher=new Launcher();
        var playback=new LibraryPlaybackService(library,catalog,launcher,validator);
        Assert.True((await playback.PlayAsync(item.Id,new(1))).Started); Assert.Equal(1,(await catalog.FindByIdAsync(item.Id))!.PlayCount);
        launcher.Success=false; Assert.False((await playback.PlayAsync(item.Id,new(1))).Started); Assert.Equal(1,(await catalog.FindByIdAsync(item.Id))!.PlayCount);
        Assert.False((await playback.PlayAsync("absent",new(1))).Started);
        var imports=new LocalLibraryImportService(paths,library,validator,validator,new(),new());
        await imports.ImportAsync(new([file])); var imported=(await library.QueryAsync(new())).Items.Single(x=>x.IsImported);
        await library.RenameAsync(imported.Id,"Vídeo antigo importado",new(2020,1,1));
        var configuration=(IContentCleanupConfigurationService)new SqliteConfigurationService(paths); await configuration.SaveAsync(new(true,1,0));
        await new LocalContentCleanupService(catalog,paths,configuration,library).CleanIfDueAsync(new(2026,10,7));
        Assert.True(File.Exists(imported.LocalPath)); Assert.Null(await catalog.FindByIdAsync(item.Id)); Assert.NotNull(await library.FindAsync(imported.Id));
    }
    internal sealed class Launcher : IPlaybackLauncher
    {
        public bool Success {get;set;}=true;
        public int Launches {get;private set;}
        public PlaybackLaunchOptions? Output {get;private set;}
        public Task<PlaybackLaunchResult> LaunchAsync(string filePath,PlaybackLaunchOptions options,CancellationToken cancellationToken=default)
        { Launches++; Output=options; return Task.FromResult(new PlaybackLaunchResult(Success,"MPV",Success ? "Aberto" : "Falhou")); }
    }
}
