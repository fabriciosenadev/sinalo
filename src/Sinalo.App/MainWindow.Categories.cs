using System.Collections.ObjectModel;
using System.Windows;
using Sinalo.Application.Library;
using Sinalo.Domain;
using Sinalo.App.ViewModels;

namespace Sinalo.App;

public partial class MainWindow
{
    private IMediaCategoryService? _categories;
    private readonly SemaphoreSlim _categoryRefreshGate=new(1,1);
    private bool _categoriesClosed;
    public IMediaCategoryService? Categories
    {
        get=>_categories;
        init
        {
            _categories=value;
            if(value is null)return;
            value.Changed+=CategoriesChanged;
            value.MediaRemoved+=CategoryMediaRemoved;
            DataContextChanged+=CategoryContextChanged;
            Closed+=(_,_)=>{_categoriesClosed=true;value.Changed-=CategoriesChanged;value.MediaRemoved-=CategoryMediaRemoved;DataContextChanged-=CategoryContextChanged;};
        }
    }
    private async void CategoryContextChanged(object sender,DependencyPropertyChangedEventArgs e)=>await RefreshCategoriesAsync();
    private void CategoriesChanged()
    {
        if(_categoriesClosed || Dispatcher.HasShutdownStarted)return;
        Dispatcher.BeginInvoke(async()=>await RefreshCategoriesAsync());
    }
    private void CategoryMediaRemoved(IReadOnlyList<string> ids)
    {
        if(_categoriesClosed || Dispatcher.HasShutdownStarted)return;
        Dispatcher.BeginInvoke(()=>{if(DataContext is HomeViewModel home)foreach(var id in ids)home.RemoveCatalogItem(id);});
    }
    public ObservableCollection<MediaCategory> CustomCategories { get; }=[];
    public async Task RefreshCategoriesAsync()
    {
        await _categoryRefreshGate.WaitAsync();
        try
        {
        if(Categories is null || _categoriesClosed)return;
        var list=await Categories.ListAsync();CustomCategories.Clear();foreach(var cat in list.Where(x=>!x.IsSystem))CustomCategories.Add(cat);
        var removedFilter=Library?.CategoryFilter is {Id:not "*"} filter && !list.Any(x=>x.Id==filter.Id);
        if(Library is not null){await Library.LoadCategoriesAsync();await Library.RefreshAsync();}
        if(DataContext is HomeViewModel home)
        {
            if(Library is not null){home.SetCategorizedProgramItems(await Library.AllMediaAsync());home.GeneralLibraryTitle=Library.LibraryTitle;}
            var old=home.SelectedLinkedVideoDestination;
            home.LinkedVideoDestinations.Clear();foreach(var cat in list.Where(x=>!x.IsDeleting))
                home.LinkedVideoDestinations.Add(new(MediaCategory.SourceFor(cat.Id)??ContentSource.Health,cat.Name,cat.Id));
            home.SelectedLinkedVideoDestination=home.LinkedVideoDestinations.FirstOrDefault(x=>x.CategoryId==(old?.CategoryId??(old is null?null:MediaCategory.ForSource(old.Source))));
            if(removedFilter)
            {
                home.SelectGeneralLibraryWorkspace();
                home.OperationMessage="A categoria aberta foi removida. Você está na Biblioteca geral.";
            }
        }
        }
        catch(Exception error){if(DataContext is HomeViewModel home)home.OperationMessage="Não foi possível atualizar as categorias: "+error.Message;}
        finally{_categoryRefreshGate.Release();}
    }
    private async void Category_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not System.Windows.Controls.Button{Tag:MediaCategory category} || Library is null || DataContext is not HomeViewModel home)return;
        if(!await Library.ResolvePendingChangesAsync())return;
        home.SelectGeneralLibraryWorkspace();Library.CategoryFilter=Library.CategoryFilters.FirstOrDefault(x=>x.Id==category.Id);
        Library.Search="";Library.Origin=Library.Origins[0];
        home.GeneralLibraryTitle=category.Name;
        Library.ImportCategory=Library.CategoryOptions.FirstOrDefault(x=>x.Id==category.Id);Library.IsImportTab=false;
        await Library.RefreshAsync();
    }
    private void ManageCategories_Click(object sender,RoutedEventArgs e)
    {
        if(Categories is null)return;
        var initialCategoryId = DataContext is HomeViewModel { IsGeneralLibraryWorkspace: true }
            && Library?.CategoryFilter is { Id: not "*", IsSystem: false } category ? category.Id : null;
        new CategoryManagerWindow(Categories, initialCategoryId){Owner=this}.ShowDialog();
    }
    public void OpenLinkForCategory()
    {
        if(DataContext is not HomeViewModel home)return;
        var categoryId=Library?.CategoryFilter is {Id:not "*"} filter ? filter.Id : Library?.ImportCategory?.Id;
        home.SelectLinkedVideoWorkspace();home.SelectedLinkedVideoDestination=home.LinkedVideoDestinations.FirstOrDefault(x=>x.CategoryId==categoryId);
    }
}
