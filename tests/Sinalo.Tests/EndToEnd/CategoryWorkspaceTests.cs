using System.IO;
using System.Windows;
using System.Windows.Threading;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using Sinalo.Tests.Integration;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class CategoryWorkspaceTests
{
    [Fact]
    public async Task NavigationAndSelectorReloadUseSamePersistedCategoryCatalog()
    {
        using var fixture=new LibraryIntegrationTests();await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root);var library=new SqliteLibraryRepository(paths);
        var service=new SqliteMediaCategoryService(paths,library,new ContentOperationGate(),new PlaybackActivityGate());
        var category=await service.CreateAsync("Culto jovem");
        for(var index=0;index<2;index++)
        {
            var file=fixture.Video($"selection-{index}.mp4");
            await library.SaveImportedAsync(new("local-"+Guid.NewGuid().ToString("N"),$"Vídeo {index}",null,null,null,file,file,MediaStorageMode.Referenced,MediaAvailability.Available,DateTimeOffset.UtcNow,CategoryId:category.Id));
        }
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            var dispatcher=Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async()=>
            {
                try
                {
                    var home=new HomeViewModel(new SaturdayWindowService(),paths,[]);
                    var model=new LibraryViewModel(library,null!,null!,null!,null!,null!,categories:service);
                    var window=new MainWindow{Categories=service,DataContext=home,Library=model};
                    SystemThemeService.ApplyToResources(window.Resources,true);window.Show();
                    await window.RefreshCategoriesAsync();Assert.Equal(category.Id,Assert.Single(window.CustomCategories).Id);
                    var view=(LibraryView)window.FindName("GeneralLibraryWorkspace");
                    var list=(System.Windows.Controls.ListBox)view.FindName("MediaList");
                    list.SelectedItems.Add(model.Items[0]);list.SelectedItems.Add(model.Items[1]);Assert.Equal(2,list.SelectedItems.Count);
                    Assert.Contains(home.LinkedVideoDestinations,x=>x.CategoryId==category.Id);
                    model.CategoryFilter=model.CategoryFilters.Single(x=>x.Id==category.Id);window.OpenLinkForCategory();
                    Assert.True(home.IsLinkedVideoWorkspace);Assert.Equal(category.Id,home.SelectedLinkedVideoDestination!.CategoryId);
                    home.SelectedLinkedVideoDestination=home.LinkedVideoDestinations.Single(x=>x.CategoryId==category.Id);
                    await service.RenameAsync(category,"Jovens");await window.RefreshCategoriesAsync();Assert.Equal("Jovens",home.SelectedLinkedVideoDestination!.Label);
                    await service.RemoveAsync((await service.ListAsync()).Single(x=>x.Id==category.Id),"missions");await window.RefreshCategoriesAsync();Assert.Empty(window.CustomCategories);
                    Assert.Null(home.SelectedLinkedVideoDestination);
                    window.Close();var empty=new MainWindow();await empty.RefreshCategoriesAsync();empty.OpenLinkForCategory();empty.Close();
                    var manager=new CategoryManagerWindow(service);
                    manager.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Sinalo.App;component/VisualIdentity.xaml",UriKind.Relative)});
                    SystemThemeService.ApplyToResources(manager.Resources,true);manager.Show();
                    var management=(CategoryManagerViewModel)manager.DataContext;await management.RefreshAsync();
                    management.Busy=true;manager.Close();Assert.True(manager.IsVisible);management.Busy=false;
                    manager.UpdateLayout();
                    var image=new System.Windows.Media.Imaging.RenderTargetBitmap(620,580,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(manager);
                    var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../TestResults/category-manager-dark.png"));Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var file=File.Create(output))encoder.Save(file);
                    manager.Close();
                    done.SetResult();
                }
                catch(Exception e){done.SetException(e);}
                finally{dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
            });Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
