using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.App.ReleaseNotes;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;
using Sinalo.Domain;
namespace Sinalo.Tests.Unit;

public sealed class WorkspaceExperienceTests
{
    private static HomeViewModel Model(params ContentItem[] items) => new(new SaturdayWindowService(),new LocalSinaloPathService(),[
        new(ContentSource.Missions,"Informativo das Missões","https://example.test/m",AvailabilityPolicy.MonthlyFull),
        new(ContentSource.ProvaiEVede,"Provai e Vede","https://example.test/p",AvailabilityPolicy.QuarterlyFull),
        new(ContentSource.Health,"Minuto de Saúde","https://example.test/h",AvailabilityPolicy.RollingSaturday)],items);
    [Fact]
    public void Navigation_ReselectsTheSameProgramAfterATool()
    {
        var model=Model();model.SelectProgram("Minuto de Saúde");model.SelectTimerWorkspace();
        Assert.Equal("Cronômetro",model.WorkspaceTitle);Assert.False(model.ShowProgramCalendar);
        model.SelectProgram("Minuto de Saúde");
        Assert.True(model.IsLibraryWorkspace);Assert.True(model.Sources.Single(item=>item.Source==ContentSource.Health).IsSelected);
        model.SelectRaffleWorkspace();Assert.Equal("Sorteio",model.WorkspaceTitle);
        model.SelectWorshipTimerWorkspace();Assert.Equal("Cronômetro de Culto",model.WorkspaceTitle);
        model.SelectLinkedVideoWorkspace();Assert.Equal("Adicionar vídeo por link",model.WorkspaceTitle);Assert.DoesNotContain(model.Sources,item=>item.IsSelected);
        model.SelectProgram("Todos");Assert.Equal("Programas de vídeo",model.WorkspaceTitle);
        model.IsCompactWorkspace=true;Assert.False(model.ShowProgramCalendar);model.IsCompactWorkspace=false;Assert.True(model.ShowProgramCalendar);
    }
    [Fact]
    public void Catalog_SearchAndDetailStates_PreserveIdentityOnRefresh()
    {
        var item=new ContentItem("1",ContentSource.Missions,"O sonho de Enoc — Parte 1",new(2026,8,8),new("https://example.test/1"),[],SyncState.Ready);
        var model=Model(item);model.SelectProgram("Informativo das Missões");
        Assert.False(model.IsCatalogEmpty);Assert.Contains("1",model.CatalogSummary);
        model.SelectedCatalogItem=model.CatalogItems[0];Assert.True(model.ShowCatalogDetails);
        model.DetailsOpen=false;Assert.False(model.ShowCatalogDetails);
        model.MarkItemPinned(item with{IsPinned=true});Assert.True(model.HasSelectedItem);
        model.MarkItemAsPlayed(item with{PlayCount=2});Assert.Equal("1",model.SelectedCatalogItem?.Id);
        model.SearchQuery="08/08";Assert.Single(model.CatalogItems);
        model.SearchQuery="inexistente";Assert.True(model.IsCatalogEmpty);Assert.Contains("pesquisa",model.CatalogEmptyMessage);Assert.False(model.ShowCatalogDetails);
        model.SearchQuery="";model.SelectProgram("Minuto de Saúde");Assert.Contains("Nenhum vídeo local",model.CatalogEmptyMessage);
        model.SelectedPlaybackScreen=null;Assert.Contains("Nenhuma tela",model.SelectedPlaybackLabel);
    }
    [Fact]
    public void DownloadFeedback_DoesNotOverwriteTheActiveTool()
    {
        var model=Model();model.SelectTimerWorkspace();model.OperationMessage="Cronômetro pausado";
        var item=new ContentItem("d",ContentSource.Health,"Teste",new(2026,8,8),new("https://example.test"),[]);
        model.ReportDownloadProgress(new(item,10,100,"Baixando"));
        Assert.Equal("Cronômetro pausado",model.OperationMessage);Assert.Contains("Baixando",model.SynchronizationMessage);
        model.SelectProgram("Minuto de Saúde");model.ReportDownloadProgress(new(item,20,100,"Baixando"));Assert.Contains("Baixando",model.OperationMessage);
    }
    [Fact]
    public void LinkSummary_IsInvalidatedWithTheUrl()
    {
        var model=Model();model.LinkedVideoUrl="https://youtu.be/123";
        Assert.False(model.HasLinkedVideo);Assert.Contains("Consulte",model.LinkedVideoSummary);
        model.SetInspectedLinkedVideo(new("123","Teste",new("https://example.test/123"),new(2026,8,8),[new("1","2",720,null)]));
        Assert.True(model.HasLinkedVideo);
        model.SelectedLinkedVideoDestination=model.LinkedVideoDestinations[1];
        model.LinkedVideoDateText="08/08/2026";
        Assert.Contains("Provai e Vede",model.LinkedVideoSummary);Assert.True(model.CanQueueLinkedVideo);
        model.LinkedVideoUrl="https://youtu.be/outro";Assert.False(model.HasLinkedVideo);Assert.False(model.CanQueueLinkedVideo);
    }
    [Fact]
    public void News_UsesTheInstalledVersionAndExcludesDrafts()
    {
        var doc=ReleaseNotesParser.Parse("## Em desenvolvimento\n- Não publicado\n## 1.1.0 - 01/10/2026\n### Novo\n- Vídeo por link\n## 1.0.0 - 01/09/2026\n### Novo\n- Estável");
        var model=new ReleaseNotesViewModel(doc,"1.0.0");
        Assert.Equal("1.1.0",model.Versions[0].Version);
        Assert.False(model.Versions[0].IsExpanded);Assert.True(model.Versions[1].IsExpanded);Assert.Contains("Instalada",model.Versions[1].Heading);
        Assert.Contains("offline",model.CurrentVersionLabel);
        model.Versions[0].IsExpanded=true;Assert.True(model.Versions[0].IsExpanded);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeaderContrast_MeetsNormalTextTarget(bool dark)
    {
        var p=SystemThemeService.GetPalette(dark);
        static double Lum(string value){var color=System.Windows.Media.ColorConverter.ConvertFromString(value);var c=(System.Windows.Media.Color)color!;static double Ch(byte b){var n=b/255d;return n<=0.04045?n/12.92:Math.Pow((n+0.055)/1.055,2.4);}return .2126*Ch(c.R)+.7152*Ch(c.G)+.0722*Ch(c.B);}
        var bg=Lum(p["Brush.Header"]);var fg=Lum(p["Brush.HeaderText"]);
        Assert.True((Math.Max(bg,fg)+.05)/(Math.Min(bg,fg)+.05)>=4.5);
        Assert.Contains("Brush.Selection",p.Keys);Assert.Contains("Brush.Error",p.Keys);
    }
    [Fact]
    public void Reload_PreservesToolsSelectionScheduleAndBackgroundFeedback()
    {
        var item=new ContentItem("saved",ContentSource.Missions,"Vídeo salvo",new(2026,8,8),new("https://example.test"),[],SyncState.Ready,true,@"C:\Videos\saved.mp4");
        var previous=Model(item);previous.SelectProgram("Informativo das Missões");previous.SelectedCatalogItem=previous.CatalogItems[0];previous.AddSelectedToSchedule();
        previous.SelectTimerWorkspace();previous.OperationMessage="Cronômetro em execução";previous.IsPresentationOpen=true;
        previous.IsUpdateAvailable=true;previous.IsUpdateReady=true;previous.UpdateMessage="Atualização pronta";
        previous.SynchronizationMessage="Validando";previous.IsSynchronizationIndeterminate=true;
        var current=Model(item);current.SelectedSource=previous.SelectedSource;current.RestoreLinkedVideoState(previous);current.RestoreWorkspaceState(previous);
        Assert.True(current.IsTimerWorkspace);Assert.Equal("saved",current.SelectedCatalogItem?.Id);Assert.Single(current.ScheduleItems);
        Assert.Equal(1,current.ScheduleItems[0].Position);Assert.False(current.ScheduleItems[0].CanMoveUp);Assert.False(current.ScheduleItems[0].CanMoveDown);
        Assert.Equal("Cronômetro em execução",current.OperationMessage);Assert.True(current.IsPresentationOpen);Assert.True(current.IsUpdateReady);Assert.Equal("Validando",current.SynchronizationMessage);
        previous.SelectProgram("Informativo das Missões");previous.DetailsOpen=false;
        current.RestoreWorkspaceState(previous);Assert.False(current.DetailsOpen);
        var empty=Model();empty.RestoreWorkspaceState(previous);Assert.Null(empty.SelectedCatalogItem);Assert.False(empty.ShowCatalogDetails);
    }
    [Fact]
    public void Schedule_PositionsAndBoundaryActionsFollowReordering()
    {
        var model=Model();Assert.True(model.IsScheduleEmpty);
        var first=new ScheduleCard("1","Primeiro","Missões","Pronto");var last=new ScheduleCard("2","Último","Missões","Pronto");
        model.ScheduleItems.Add(first);model.ScheduleItems.Add(last);
        Assert.False(model.IsScheduleEmpty);Assert.True(first.CanMoveDown);Assert.True(last.CanMoveUp);
        model.MoveScheduleItem(last,-1);Assert.Equal(1,last.Position);Assert.False(last.CanMoveUp);Assert.Equal(2,first.Position);
        model.RemoveFromSchedule(last);Assert.False(first.CanMoveDown);model.RemoveFromSchedule(first);Assert.True(model.IsScheduleEmpty);
    }
    [Fact]
    public void News_EmptyAndCurrentHistoryHaveNeutralLabels()
    {
        Assert.Contains("Nenhum histórico",new ReleaseNotesViewModel(ReleaseNotesParser.Parse(""),"1.1.0").LatestVersionLabel);
        Assert.Contains("inclui",new ReleaseNotesViewModel(ReleaseNotesParser.Parse("## 1.1.0 - 01/10/2026\n### Novo\n- Teste"),"1.1.0").LatestVersionLabel);
        var home=Model();home.SelectedPlaybackScreen=null;Assert.Equal("Nenhuma tela disponível",home.SelectedPlaybackLabel);
        home.SynchronizationQueueItems.Add(new("Saúde","Aguardando","",true));Assert.Equal("Downloads (1)",home.DownloadsLabel);
    }
    [Fact]
    public void BackgroundDownload_DoesNotReplaceTheOperatorsSelectionOrOpenDetails()
    {
        var item=new ContentItem("1",ContentSource.Missions,"Selecionado",new(2026,8,8),new("https://example.test"),[],SyncState.Ready);
        var model=Model(item);model.SelectedCatalogItem=model.CatalogItems[0];model.DetailsOpen=false;
        model.MarkItemAsReady(item with{Id="2",Title="Novo vídeo"});
        Assert.Equal("1",model.SelectedCatalogItem?.Id);Assert.False(model.DetailsOpen);
        var empty=Model();empty.MarkItemAsReady(item);Assert.Equal("1",empty.SelectedCatalogItem?.Id);Assert.False(empty.DetailsOpen);
        var filtered=Model();filtered.SearchQuery="inexistente";filtered.MarkItemAsReady(item);Assert.Null(filtered.SelectedCatalogItem);
    }
    [Fact]
    public void Timer_ActionLabels_DistinguishStartPauseAndResume()
    {
        var model=Model();Assert.Equal("Iniciar",model.Timer.StartPauseLabel);
        model.Timer.StartOrPause();Assert.Equal("Pausar",model.Timer.StartPauseLabel);
        model.Timer.StartOrPause();Assert.Equal("Continuar",model.Timer.StartPauseLabel);
        model.Timer.Reset();Assert.Equal("Iniciar",model.Timer.StartPauseLabel);
    }
}
