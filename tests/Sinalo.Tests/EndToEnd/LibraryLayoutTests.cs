using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Application.Services;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class LibraryLayoutTests
{
    [Theory]
    [InlineData(false,1280,720,1.0,false)]
    [InlineData(true,1280,720,1.0,false)]
    [InlineData(false,1920,1080,1.0,false)]
    [InlineData(true,1920,1080,1.0,false)]
    [InlineData(false,1280,720,1.25,false)]
    [InlineData(true,1280,720,1.5,false)]
    [InlineData(true,960,580,1.0,false)]
    [InlineData(true,1024,768,1.5,false)]
    [InlineData(true,1280,720,1.5,true)]
    [InlineData(false,1024,768,1.5,true)]
    public async Task FullLibraryWithRowsDetailsAndImportFitsOperatorViewport(bool dark,int width,int height,double scale,bool active)
    {
        var repository = new LibraryPreviewRepository();
        var model = new LibraryViewModel(repository,null!,null!,null!,null!,null!);
        await model.RefreshAsync();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
                var resources=new ResourceDictionary();
                SystemThemeService.ApplyToResources(resources,dark);
                var home=new HomeViewModel(new SaturdayWindowService(),new LocalSinaloPathService(rootPath:Path.Combine(Path.GetTempPath(),"Sinalo.Layout")),[]);
                home.SelectGeneralLibraryWorkspace();
                var window=new MainWindow { Library=model, DataContext=home, Width=width, Height=height, Resources=resources };
                resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml",UriKind.Relative) });
                if (active)
                {
                    var controller = new Sinalo.Tests.Unit.RecordingPlaybackController();
                    controller.Update(Sinalo.Tests.Unit.PlaybackControllerTests.Active());
                    window.SetValue(MainWindow.PlaybackProperty,new PlaybackViewModel(controller,new Sinalo.Application.Playback.PlaybackService(null!,controller)));
                }
                var frame=(FrameworkElement)window.Content; frame.LayoutTransform=new ScaleTransform(scale,scale);
                window.Show(); window.Width=width; window.Height=height; window.UpdateLayout();
                try
                {
                    var view=(LibraryView)window.FindName("GeneralLibraryWorkspace");
                    var list=(ListBox)view.FindName("MediaList");
                    Assert.Equal(Visibility.Collapsed,((FrameworkElement)window.FindName("SaturdayHeader")).Visibility);
                    Assert.Equal(Visibility.Collapsed,((FrameworkElement)window.FindName("ProgramWorkspace")).Visibility);
                    Assert.Equal("Biblioteca de vídeos",home.WorkspaceTitle);
                    Assert.Equal(40,list.Items.Count);
                    Save(window,root,dark,width,scale,"videos");
                    Assert.True(list.ActualHeight>65,$"List height {list.ActualHeight}");
                    Assert.Equal(ScrollUnit.Pixel,VirtualizingPanel.GetScrollUnit(list));
                    Assert.True(VirtualizingPanel.GetIsVirtualizing(list));
                    var scrolling=VisualChildren(list).OfType<ScrollViewer>().First();
                    scrolling.ScrollToVerticalOffset(7);window.UpdateLayout();
                    Assert.InRange(scrolling.VerticalOffset,6.5,7.5);
                    scrolling.ScrollToTop();window.UpdateLayout();
                    if (width == 1024 && scale == 1.5)
                    {
                        var filters = (Expander)view.FindName("FilterOptions");
                        Assert.False(filters.IsExpanded);
                        filters.IsExpanded = true; window.UpdateLayout();
                        Assert.True(((TextBox)view.FindName("SearchInput")).IsVisible);
                        Assert.Contains(home.PlaybackScreens,screen => screen.Label == model.OutputScreenLabel);
                        filters.IsExpanded = false; window.UpdateLayout();
                        Assert.True(list.ActualHeight>65);
                    }
                    Assert.Equal(home.SelectedPlaybackScreen!.Label,model.OutputScreenLabel);

                    model.Selected=model.Items[0]; model.DetailsOpen=true; window.UpdateLayout();
                    Assert.True(model.ShowDetails);
                    Assert.Equal(model.IsWideLayout,model.ShowList);
                    var details=(FrameworkElement)view.FindName("DetailsRegion");
                    Assert.True(details.ActualHeight>140);
                    Assert.True(details.ActualWidth>260);
                    var detailsScroll=(ScrollViewer)view.FindName("DetailsScroll");
                    Assert.True(detailsScroll.ViewportHeight>60,$"Details viewport {detailsScroll.ViewportHeight}");
                    detailsScroll.ScrollToEnd(); window.UpdateLayout(); Assert.True(detailsScroll.VerticalOffset>0);
                    detailsScroll.ScrollToTop(); window.UpdateLayout();
                    Save(window,root,dark,width,scale,"details");
                    model.EditInformationCommand.Execute(null); model.EditName="Nome editado sem perder a seleção"; window.UpdateLayout();
                    Save(window,root,dark,width,scale,"edit");

                    model.ShowImportsCommand.Execute(null); window.UpdateLayout();
                    Assert.True(model.IsImportTab); Assert.False(model.ShowList); Assert.False(model.ShowDetails);
                    model.ImportMessage="Importação concluída."; model.HasImportResult=true; model.ImportedCount=12; model.ExistingCount=3;
                    Save(window,root,dark,width,scale,"import");
                    model.ShowVideoListCommand.Execute(null); window.UpdateLayout();
                    Assert.True(model.IsEditing); Assert.Equal("Nome editado sem perder a seleção",model.EditName);
                    model.CancelEditingCommand.Execute(null);
                    home.SelectTimerWorkspace(); window.UpdateLayout();
                    Assert.Equal(Visibility.Collapsed,view.Visibility);
                    Assert.Equal("Cronômetro",home.WorkspaceTitle);
                }
                finally { window.Close(); }
            }
            catch(Exception exception) { failure=exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    private static void Save(Window window,string root,bool dark,int width,double scale,string mode)
    {
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        image.Render(window);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        Directory.CreateDirectory(Path.Combine(root,"TestResults"));
        using var output=File.Create(Path.Combine(root,"TestResults",$"library-{(dark ? "dark":"light")}-{width}-{scale:0.00}-{mode}.png"));
        encoder.Save(output);
    }
    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject element)
    {
        yield return element;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(element);index++)
            foreach(var child in VisualChildren(VisualTreeHelper.GetChild(element,index))) yield return child;
    }
}

internal sealed class LibraryPreviewRepository : ILibraryRepository
{
    public List<LibraryMedia> Media {get;} = Enumerable.Range(0,40).Select(i=>new LibraryMedia(
        "preview-"+i,i%4==0 ? "O sonho de Enoc — Parte 2: uma história para apresentar à igreja" : "Vídeo de apresentação "+(i+1),
        i%4==3 ? null:"content-"+i,i%4==3 ? null:(ContentSource?)(i%3),i%4==3 ? null:new DateOnly(2026,10,10).AddDays(-i*7),
        @"C:\Vídeos da igreja\Programação do sábado\Um caminho extenso para testar localização de arquivo\video.mp4",null,
        i%4==3 ? MediaStorageMode.Referenced:MediaStorageMode.Managed,i%5==0 ? MediaAvailability.Missing:MediaAvailability.Available,
        DateTimeOffset.UtcNow,PlayCount:i,IsPinned:i%2==0)).ToList();
    public Task<LibraryPage> QueryAsync(LibraryQuery query,CancellationToken token=default)
    {
        var result=Media.Where(x=>x.Name.Contains(query.Search,StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult(new LibraryPage(result.Skip(query.Page*query.PageSize).Take(query.PageSize).ToArray(),result.Length));
    }
    public Task<LibraryMedia?> FindAsync(string id,CancellationToken token=default)=>Task.FromResult(Media.Find(x=>x.Id==id));
    public Task<LibraryMedia?> FindByPathAsync(string path,CancellationToken token=default)=>Task.FromResult(Media.Find(x=>x.LocalPath==path));
    public Task SaveImportedAsync(LibraryMedia media,CancellationToken token=default) { Media.Add(media); return Task.CompletedTask; }
    public Task RenameAsync(string id,string name,DateOnly? date,CancellationToken token=default) { var index=Media.FindIndex(x=>x.Id==id); Media[index]=Media[index] with {Name=name,UsageDate=date}; return Task.CompletedTask; }
    public Task RelinkAsync(string id,string path,long size,string hash,CancellationToken token=default)=>Task.CompletedTask;
    public Task RemoveImportedAsync(string id,CancellationToken token=default) { Media.RemoveAll(x=>x.Id==id); return Task.CompletedTask; }
    public Task RecordPlaybackAsync(string id,CancellationToken token=default)=>Task.CompletedTask;
    public Task RelocateAsync(string previousRoot,string nextRoot,CancellationToken token=default)=>Task.CompletedTask;
    public Task<int> CountPathReferencesAsync(string path,CancellationToken token=default)=>Task.FromResult(1);
}
