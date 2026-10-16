using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using Sinalo.Tests.Integration;
using Sinalo.Tests.Unit;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class CategoryStabilizationTests
{
    [Theory]
    [InlineData(true,0)][InlineData(false,0)]
    [InlineData(true,1)][InlineData(false,1)]
    [InlineData(true,2)][InlineData(false,2)]
    public Task NavigationRendersNamesAndManagerSelectsOnlyCurrentCategory(bool dark,int context)=>OnUi(async()=>
    {
        using var fixture=new LibraryIntegrationTests();await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root);
        var service=new CategoryTestService();
        service.Rows[1]=service.Rows[1] with {IsDeleting=true};
        var home=new HomeViewModel(new SaturdayWindowService(),paths,[]);
        var model=new LibraryViewModel(new SqliteLibraryRepository(paths),null!,null!,null!,null!,null!,categories:service);
        var main=new MainWindow{Categories=service,DataContext=home,Library=model};
        Theme(main,dark);main.Show();
        try
        {
            await main.RefreshCategoriesAsync();main.UpdateLayout();
            var navigation=(ItemsControl)main.FindName("CustomCategoryNavigation");
            var buttons=Descendants<Button>(navigation).Where(x=>x.Tag is MediaCategory).ToArray();
            Assert.Equal(2,buttons.Length);
            foreach(var button in buttons)
            {
                var category=(MediaCategory)button.Tag;
                Assert.Contains(Descendants<TextBlock>(button),x=>x.Text==category.ToString());
                Assert.DoesNotContain(Descendants<TextBlock>(button),x=>x.Text.Contains("System.Windows.Controls"));
            }
            buttons.Single(x=>((MediaCategory)x.Tag).Id=="a").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(()=>home.IsGeneralLibraryWorkspace&&model.CategoryFilter?.Id=="a");
            if(context==1)model.CategoryFilter=model.CategoryFilters.Single(x=>x.Id=="*");
            if(context==2)home.SelectLinkedVideoWorkspace();
            Exception? failure=null;bool inspected=false;
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
            var deadline=DateTime.UtcNow.AddSeconds(8);
            timer.Tick+=(_,_)=>
            {
                var manager=main.OwnedWindows.OfType<CategoryManagerWindow>().FirstOrDefault();
                try
                {
                    if(DateTime.UtcNow>deadline)throw new TimeoutException("Category manager did not finish loading.");
                    if(manager?.DataContext is not CategoryManagerViewModel vm||vm.Busy)return;
                    Assert.Equal(context==0?"a":null,vm.Selected?.Id);
                    Assert.Equal(context==0,vm.CanDelete);
                    Assert.Equal(context==0?"A":"",vm.Name);
                    inspected=true;timer.Stop();manager.Close();
                }
                catch(Exception error){failure=error;timer.Stop();manager?.Close();}
            };
            timer.Start();
            try {((Button)main.FindName("ManageCategoriesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
            finally {timer.Stop();}
            if(failure is not null)throw failure;
            Assert.True(inspected);Assert.Equal(0,service.RemoveCalls);
        }
        finally {main.Close();}
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is T match)yield return match;
            foreach(var nested in Descendants<T>(child))yield return nested;
        }
    }

    [Theory]
    [InlineData(0,false)][InlineData(0,true)][InlineData(1,false)][InlineData(1,true)]
    [InlineData(2,false)][InlineData(2,true)][InlineData(3,false)][InlineData(3,true)]
    public async Task IntegratedRemovalMatrixPreservesReferencesAndRefreshesAllDestinations(int setup,bool transfer)
    {
        using var fixture=new LibraryIntegrationTests();await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root);var repository=new SqliteLibraryRepository(paths);
        var activity=new PlaybackActivityGate();var gate=new ContentOperationGate();
        using var operationState=new CategoryOperationState();
        var service=new SqliteMediaCategoryService(paths,repository,gate,activity,()=>operationState.IsBusy);
        var category=await service.CreateAsync("Categoria de teste");await service.CreateAsync("Outra");
        var originals=new List<string>();var media=new List<LibraryMedia>();
        if(setup>0)
        {
            var validator=new Mp4LibraryValidator(activity);
            var importer=new LocalLibraryImportService(paths,repository,validator,validator,gate,activity);
            foreach(var mode in new[]{MediaStorageMode.Managed,MediaStorageMode.Referenced})
            {
                var path=fixture.Video(mode+".mp4");originals.Add(path);
                await importer.ImportAsync(new([path],mode,CategoryId:category.Id));media.Add((await repository.FindByPathAsync(path))!);
            }
            await repository.RecordPlaybackAsync(media[0].Id);
        }
        await OnUi(async()=>
        {
            var home=new HomeViewModel(new SaturdayWindowService(),paths,[]);
            var library=new LibraryViewModel(repository,null!,null!,null!,null!,null!,categories:service);
            var main=new MainWindow{Categories=service,Library=library,DataContext=home};Theme(main,true);main.Show();
            main.SynchronizationQueue=new SynchronizationQueue((_,_,_)=>Task.FromResult(new SynchronizationQueueCompletion(0)));
            operationState.Bind(library,main.SynchronizationQueue);
            await main.RefreshCategoriesAsync();
            library.CategoryFilter=library.CategoryFilters.Single(x=>x.Id==category.Id);
            library.ImportCategory=library.CategoryOptions.Single(x=>x.Id==category.Id);
            library.MoveCategory=library.ImportCategory;home.SelectGeneralLibraryWorkspace();main.OpenLinkForCategory();
            if(setup==2)
            {
                await service.RenameAsync(category,"Renomeada");await service.ReorderAsync(category.Id,1);
                await main.RefreshCategoriesAsync();
            }
            if(setup==3)
            {
                library.Search="inexistente";await library.RefreshAsync();
                home.SelectProgram("Provai e Vede");home.SelectGeneralLibraryWorkspace();
                library.Search="";await library.RefreshAsync();
            }
            // Same window and production event subscriptions used by the App bootstrap.
            var manager=new CategoryManagerWindow(service){Owner=main};Theme(manager,true);manager.Show();
            var vm=(CategoryManagerViewModel)manager.DataContext;
            await Until(()=>!vm.Busy);await vm.RefreshAsync();vm.Selected=vm.Items.Single(x=>x.Id==category.Id);
            var request=(await vm.PrepareRemovalAsync())!;
            if(transfer && !request.IsEmpty){request.IsTransfer=true;request.Destination=request.Destinations.Single(x=>x.Id=="health");}
            else request.IsDelete=true;
            await vm.CompleteRemovalAsync(request);Assert.Contains("excluída",vm.Message);
            await main.RefreshCategoriesAsync();
            Assert.DoesNotContain(main.CustomCategories,x=>x.Id==category.Id);
            Assert.DoesNotContain(library.CategoryOptions,x=>x.Id==category.Id);
            Assert.DoesNotContain(home.LinkedVideoDestinations,x=>x.CategoryId==category.Id);
            Assert.Null(home.SelectedLinkedVideoDestination);Assert.Equal("*",library.CategoryFilter!.Id);
            Assert.Equal("Biblioteca de vídeos",home.WorkspaceTitle);
            manager.Close();
            var reopened=new CategoryManagerWindow(service){Owner=main};Theme(reopened,false);reopened.Show();
            var reopenedVm=(CategoryManagerViewModel)reopened.DataContext;await Until(()=>!reopenedVm.Busy);await reopenedVm.RefreshAsync();
            Assert.DoesNotContain(reopenedVm.Items,x=>x.Id==category.Id);reopened.Close();operationState.Dispose();main.Close();
        });
        foreach(var original in originals)Assert.True(File.Exists(original));
        foreach(var item in media)
        {
            var after=await repository.FindAsync(item.Id);
            if(transfer){Assert.NotNull(after);Assert.Equal("health",after.CategoryId);Assert.Equal(item.LocalPath,after.LocalPath);Assert.Equal(item.StorageMode,after.StorageMode);}
            else{Assert.Null(after);if(item.StorageMode==MediaStorageMode.Managed)Assert.False(File.Exists(item.LocalPath));}
        }
    }

    [Theory][InlineData(false)][InlineData(true)]
    public Task ProductionOperationStateBlocksActiveWorkAndAllowsRetry(bool importing)=>OnUi(async()=>
    {
        using var fixture=new LibraryIntegrationTests();await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root);var repository=new SqliteLibraryRepository(paths);
        var gate=new ContentOperationGate();var activity=new PlaybackActivityGate();
        using var state=new CategoryOperationState();
        var service=new SqliteMediaCategoryService(paths,repository,gate,activity,()=>state.IsBusy);
        var category=await service.CreateAsync("Ocupada");
        var library=new LibraryViewModel(repository,null!,null!,null!,null!,null!,categories:service);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue=new SynchronizationQueue(async(_,_,token)=>{await release.Task.WaitAsync(token);return new(0);});
        var main=new MainWindow{Library=library,Categories=service,DataContext=new HomeViewModel(new SaturdayWindowService(),paths,[])};
        Theme(main,true);main.SynchronizationQueue=queue;state.Bind(library,queue);main.Show();
        try
        {
            if(importing)library.IsImporting=true;
            else queue.Enqueue(new(new SourceConfiguration(ContentSource.Health,"Saúde","https://example.test/",AvailabilityPolicy.RollingSaturday)));
            Assert.True(await Task.Run(()=>state.IsBusy));
            var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RemoveAsync(category,null));
            Assert.Contains("downloads/importações",failure.Message);
            Assert.Contains(await service.ListAsync(),x=>x.Id==category.Id&&!x.IsDeleting);
            library.IsImporting=false;release.TrySetResult();await queue.WhenIdleAsync();
            Assert.False(await Task.Run(()=>state.IsBusy));
            await service.RemoveAsync(category,null);await main.RefreshCategoriesAsync();
            Assert.DoesNotContain(main.CustomCategories,x=>x.Id==category.Id);
            Assert.DoesNotContain(library.CategoryOptions,x=>x.Id==category.Id);
            // Disposal removes the subscription rather than retaining a closed UI.
            state.Dispose();library.IsImporting=true;Assert.False(await Task.Run(()=>state.IsBusy));
            library.IsImporting=false;
        }
        finally{release.TrySetResult();queue.CancelAll();await queue.WhenIdleAsync();main.Close();}
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task RealDeleteButtonUsesProductionGuardAndPersistsOnlyAfterConfirmation(bool confirm)=>OnUi(async()=>
    {
        using var fixture=new LibraryIntegrationTests();await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root);var repository=new SqliteLibraryRepository(paths);
        using var state=new CategoryOperationState();
        var service=new SqliteMediaCategoryService(paths,repository,new ContentOperationGate(),new PlaybackActivityGate(),()=>state.IsBusy);
        var category=await service.CreateAsync("Categoria da confirmação");
        var library=new LibraryViewModel(repository,null!,null!,null!,null!,null!,categories:service);
        var queue=new SynchronizationQueue((_,_,_)=>Task.FromResult(new SynchronizationQueueCompletion(0)));
        state.Bind(library,queue);
        var main=new MainWindow{Categories=service,Library=library,DataContext=new HomeViewModel(new SaturdayWindowService(),paths,[])};
        Theme(main,true);main.Show();await main.RefreshCategoriesAsync();
        var manager=new CategoryManagerWindow(service,category.Id){Owner=main};Theme(manager,true);manager.Show();
        try
        {
            var vm=(CategoryManagerViewModel)manager.DataContext;
            await Until(()=>!vm.Busy&&vm.Selected?.Id==category.Id);
            bool handled=false;Exception? failure=null;
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>
            {
                if(manager.OwnedWindows.OfType<CategoryRemovalWindow>().FirstOrDefault() is not {} dialog)return;
                timer.Stop();handled=true;
                try
                {
                    if(confirm)((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else dialog.DialogResult=false;
                }
                catch(Exception error){failure=error;dialog.Close();}
            };
            timer.Start();
            try
            {
                ((Button)manager.FindName("DeleteCategoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(()=>handled&&!vm.Busy);
            }
            finally{timer.Stop();}
            if(failure is not null)throw failure;
            Assert.Contains(confirm?"excluída":"cancelada",vm.Message);
            Assert.Equal(!confirm,(await service.ListAsync()).Any(x=>x.Id==category.Id));
            await main.RefreshCategoriesAsync();
            Assert.Equal(!confirm,main.CustomCategories.Any(x=>x.Id==category.Id));
            Assert.Equal(!confirm,library.CategoryOptions.Any(x=>x.Id==category.Id));
        }
        finally{manager.Close();main.Close();}
    });

    [Fact]
    public Task ProductionOperationStateIsRecheckedAfterWaitingForFileGate()=>OnUi(async()=>
    {
        using var fixture=new LibraryIntegrationTests();await fixture.InitializeAsync();
        var paths=new LocalSinaloPathService(rootPath:fixture.Root);var repository=new SqliteLibraryRepository(paths);
        var gate=new ContentOperationGate();using var state=new CategoryOperationState();
        var library=new LibraryViewModel(repository,null!,null!,null!,null!,null!);
        var queue=new SynchronizationQueue((_,_,_)=>Task.FromResult(new SynchronizationQueueCompletion(0)));
        state.Bind(library,queue);
        var checkedBeforeGate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service=new SqliteMediaCategoryService(paths,repository,gate,new PlaybackActivityGate(),()=>
        {
            var busy=state.IsBusy;checkedBeforeGate.TrySetResult();return busy;
        });
        var category=await service.CreateAsync("Aguardando");
        var lease=await gate.EnterAsync();
        var removal=service.RemoveAsync(category,null);
        try
        {
            await checkedBeforeGate.Task.WaitAsync(TimeSpan.FromSeconds(8));
            library.IsImporting=true;
        }
        finally{lease.Dispose();}
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>removal);
        Assert.Contains("operações pendentes",error.Message);
        Assert.Contains(await service.ListAsync(),x=>x.Id==category.Id&&!x.IsDeleting);
        library.IsImporting=false;await service.RemoveAsync(category,null);
        Assert.DoesNotContain(await service.ListAsync(),x=>x.Id==category.Id);
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task ActualButtonsDriveRemovalDialogAndPreserveCancellation(bool confirm)=>OnUi(async()=>
    {
        var service=new CategoryTestService();
        var manager=new CategoryManagerWindow(service);Theme(manager,false);manager.Show();
        var vm=(CategoryManagerViewModel)manager.DataContext;await Until(()=>!vm.Busy);
        vm.Selected=vm.Items[0];
        Exception? callbackError=null;bool handled=false;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>
        {
            if(handled || manager.OwnedWindows.OfType<CategoryRemovalWindow>().FirstOrDefault() is not {} dialog)return;
            handled=true;timer.Stop();
            try
            {
                dialog.UpdateLayout();
                Assert.True(((System.Windows.Controls.Button)dialog.FindName("CancelButton")).IsDefault);
                var action=(System.Windows.Controls.Button)dialog.FindName(confirm?"ConfirmButton":"CancelButton");
                Assert.True(action.IsEnabled);action.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                // IsCancel normally sets DialogResult through the native command path.
                if(!confirm && dialog.IsVisible)dialog.DialogResult=false;
            }
            catch(Exception error){callbackError=error;dialog.Close();}
        };
        timer.Start();
        ((System.Windows.Controls.Button)manager.FindName("DeleteCategoryButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await Until(()=>handled&&!vm.Busy);timer.Stop();
        if(callbackError is not null)throw callbackError;
        Assert.Equal(confirm?1:0,service.RemoveCalls);Assert.Equal(confirm?1:2,vm.Items.Count);
        Assert.Contains(confirm?"excluída":"cancelada",vm.Message);manager.Close();
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task ChoiceControlsAreBoundAndCompactWindowKeepsActionsVisible(bool dark)=>OnUi(()=>
    {
        var category=new MediaCategory("a","Categoria com nome longo para teste de quebra de linha");
        var model=new CategoryRemovalViewModel(category,new(1,1,1,100,["x","y"]),[category,new("health","Minuto de Saúde"),new("p","Pendente",IsDeleting:true)]);
        var dialog=new CategoryRemovalWindow(model){Width=430,Height=340};Theme(dialog,dark);dialog.Show();dialog.UpdateLayout();
        var confirm=(System.Windows.Controls.Button)dialog.FindName("ConfirmButton");Assert.False(confirm.IsEnabled);
        var transfer=(RadioButton)dialog.FindName("TransferChoice");transfer.IsChecked=true;
        var destination=(ComboBox)dialog.FindName("DestinationInput");destination.SelectedItem=model.Destinations[0];dialog.UpdateLayout();Assert.True(confirm.IsEnabled);
        ((RadioButton)dialog.FindName("DeleteChoice")).IsChecked=true;dialog.UpdateLayout();Assert.True(model.IsDelete);Assert.True(confirm.IsEnabled);
        var footer=confirm.TranslatePoint(new Point(),dialog);Assert.True(footer.Y+confirm.ActualHeight<dialog.ActualHeight);
        Assert.Equal(40,confirm.ActualHeight);dialog.Close();return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public Task PopulatedCategoryDialogRequiresChoiceAndConfirmsOnlyIntendedOperation(bool transfer,bool approve)=>OnUi(async()=>
    {
        var service=new CategoryTestService{Impact=new(1,1,1,50,["copy","reference"])};
        var manager=new CategoryManagerWindow(service);Theme(manager,true);manager.Show();
        var vm=(CategoryManagerViewModel)manager.DataContext;await Until(()=>!vm.Busy);vm.Selected=vm.Items[0];
        int stage=0;Exception? failure=null;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>
        {
            try
            {
                if(stage==0 && manager.OwnedWindows.OfType<CategoryRemovalWindow>().FirstOrDefault() is {} dialog)
                {
                    stage=1;var request=(CategoryRemovalViewModel)dialog.DataContext;
                    var confirm=(System.Windows.Controls.Button)dialog.FindName("ConfirmButton");Assert.False(confirm.IsEnabled);
                    ((RadioButton)dialog.FindName(transfer?"TransferChoice":"DeleteChoice")).IsChecked=true;
                    if(transfer)((ComboBox)dialog.FindName("DestinationInput")).SelectedItem=request.Destinations[0];
                    dialog.UpdateLayout();Assert.True(confirm.IsEnabled);
                    if(approve || !transfer)confirm.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    else dialog.DialogResult=false;
                }
                else if(stage==1 && !transfer && manager.OwnedWindows.Cast<Window>().FirstOrDefault(x=>x.Title=="Exclusão definitiva") is {} final)
                {
                    stage=2;
                    if(approve)
                    {
                        var grid=(Grid)final.Content;
                        var actions=grid.Children.OfType<WrapPanel>().Single();
                        actions.Children.OfType<System.Windows.Controls.Button>().Single(x=>Equals(x.Content,"Excluir definitivamente")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    }
                    else final.DialogResult=false;
                }
            }
            catch(Exception error)
            {
                failure=error;stage=2;timer.Stop();foreach(Window window in manager.OwnedWindows.Cast<Window>().ToArray())window.Close();
            }
        };
        timer.Start();
        ((System.Windows.Controls.Button)manager.FindName("DeleteCategoryButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await Until(()=>stage>0&&!vm.Busy);timer.Stop();if(failure is not null)throw failure;
        Assert.Equal(approve?1:0,service.RemoveCalls);
        if(approve)Assert.Equal(transfer?"b":null,service.TransferredTo);
        else Assert.Contains("cancelada",vm.Message);
        manager.Close();
    });

    private static void Theme(Window window,bool dark)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Sinalo.App;component/VisualIdentity.xaml",UriKind.Relative)});
        SystemThemeService.ApplyToResources(window.Resources,dark);
    }
    private static async Task Until(Func<bool> predicate)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while(!predicate())await Task.Delay(10,timeout.Token);
    }
    private static async Task OnUi(Func<Task> action)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            var dispatcher=Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async()=>
            {
                try{await action();done.TrySetResult();}catch(Exception e){done.TrySetException(e);}
                finally{dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
            });Dispatcher.Run();
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(35));
    }
}
