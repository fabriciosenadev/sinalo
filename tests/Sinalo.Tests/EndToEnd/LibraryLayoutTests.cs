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

[Collection(Sinalo.Tests.Integration.SqliteIntegrationCollection.Name)]
public sealed class LibraryLayoutTests
{
    [Theory]
    [InlineData(false,1280,720,1.0)]
    [InlineData(true,1280,720,1.0)]
    [InlineData(false,1920,1080,1.0)]
    [InlineData(true,1920,1080,1.0)]
    [InlineData(false,1280,720,1.25)]
    [InlineData(true,1280,720,1.5)]
    [InlineData(true,960,580,1.0)]
    public async Task FullLibraryWithRowsDetailsAndImportFitsOperatorViewport(bool dark,int width,int height,double scale)
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
                XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var source=XDocument.Load(Path.Combine(root,"src/Sinalo.App/App.xaml"));
                var dictionary=new XElement(ns+"ResourceDictionary",new XAttribute(XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"),source.Root!.Element(ns+"Application.Resources")!.Elements());
                var resources=(ResourceDictionary)XamlReader.Parse(dictionary.ToString());
                SystemThemeService.ApplyToResources(resources,dark);
                var home=new HomeViewModel(new SaturdayWindowService(),new LocalSinaloPathService(rootPath:Path.Combine(Path.GetTempPath(),"Sinalo.Layout")),[]);
                home.SelectGeneralLibraryWorkspace();
                var window=new MainWindow { Library=model, DataContext=home, Width=width, Height=height, Resources=resources };
                var frame=(FrameworkElement)window.Content; frame.LayoutTransform=new ScaleTransform(scale,scale);
                window.Show(); window.UpdateLayout();
                try
                {
                    var view=(LibraryView)window.FindName("GeneralLibraryWorkspace");
                    var list=(ListBox)view.FindName("MediaList");
                    Assert.Equal(Visibility.Collapsed,((FrameworkElement)window.FindName("SaturdayHeader")).Visibility);
                    Assert.Equal(Visibility.Collapsed,((FrameworkElement)window.FindName("GlobalActionsSidebar")).Visibility);
                    Assert.Equal(2,Grid.GetColumnSpan((FrameworkElement)window.FindName("WorkspaceRegion")));
                    Assert.Equal(40,list.Items.Count);
                    Save(window,root,dark,width,scale,"videos");
                    Assert.True(list.ActualHeight>65,$"List height {list.ActualHeight}");
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
                    Assert.Equal(Visibility.Visible,((FrameworkElement)window.FindName("SaturdayHeader")).Visibility);
                    Assert.Equal(Visibility.Visible,((FrameworkElement)window.FindName("GlobalActionsSidebar")).Visibility);
                    Assert.Equal(1,Grid.GetColumnSpan((FrameworkElement)window.FindName("WorkspaceRegion")));
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
