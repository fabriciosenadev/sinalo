using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sinalo.App.ViewModels;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.ComponentModel;
using System.Windows.Data;

namespace Sinalo.App;

public partial class LibraryView : System.Windows.Controls.UserControl
{
    private LibraryViewModel? _model;
    private bool _selecting;
    private bool? _compactLayout;
    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachModel();
        Unloaded += (_, _) => { if (_model is not null) _model.PropertyChanged -= ModelChanged; };
    }
    private void AttachModel()
    {
        if (_model is not null) _model.PropertyChanged -= ModelChanged;
        _model = DataContext as LibraryViewModel;
        if (_model is null) return;
        _model.PropertyChanged += ModelChanged;
        _model.ConfirmPendingChanges = ConfirmChanges;
        UpdateWorkspaceLayout();
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private LibraryEditDecision ConfirmChanges() => UiDialog.ChooseUnsavedChanges(Window.GetWindow(this)) switch
    { UiDialog.UnsavedChoice.Save => LibraryEditDecision.Save, UiDialog.UnsavedChoice.Discard => LibraryEditDecision.Discard, _ => LibraryEditDecision.Cancel };
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(LibraryViewModel.Selected) && !_selecting && MediaList.SelectedItems.Count<=1 && _model is not null)
        {
            _selecting=true;try{MediaList.SelectedItem=_model.Selected;}finally{_selecting=false;}
        }
        if (e.PropertyName is nameof(LibraryViewModel.ShowDetails) or nameof(LibraryViewModel.IsWideLayout)) UpdateWorkspaceLayout();
    }
    private void Workspace_Loaded(object sender, RoutedEventArgs e) { AttachModel(); (Window.GetWindow(this) as MainWindow)?.RefreshLibraryContext(); }
    private void Workspace_SizeChanged(object sender, SizeChangedEventArgs e)
    { if (_model is not null) _model.IsWideLayout = ActualWidth >= 1100; UpdateWorkspaceLayout(); }
    public void UpdateWorkspaceLayout()
    {
        if (_model is null || DetailsColumn is null) return;
        var wide = _model.IsWideLayout;
        DetailsColumn.Width = wide && _model.ShowDetails ? new GridLength(360) : new GridLength(0);
        Grid.SetColumn(DetailsRegion, wide ? 1 : 0);
        Grid.SetColumnSpan(DetailsRegion, wide ? 1 : 2);
        VideoListRegion.Margin = wide && _model.ShowDetails ? new Thickness(0,0,16,0) : new Thickness(0);
        var shortViewport = ActualHeight < 500;
        var compact = ActualHeight < 430 || ActualWidth < 650;
        LibraryHeader.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        VideosTab.Padding = ImportTab.Padding = compact ? new Thickness(12,6,12,6) : new Thickness(18,12,18,12);
        FilterOptions.Margin = new Thickness(0,0,0,compact ? 4 : 8);
        if (_compactLayout != compact)
        {
            _compactLayout = compact;
            FilterOptions.IsExpanded = !compact;
        }
        DetailsRegion.Padding = new Thickness(compact ? 8 : 18);
        LibraryBackButton.Margin = new Thickness(0,0,0,compact ? 4 : 12);
        LibraryPlayButton.Margin = new Thickness(0,compact ? 4 : 12,0,0);
        LibraryHeader.Margin = new Thickness(0,0,0,shortViewport ? 8 : 14);
        TabBar.Margin = new Thickness(0,0,0,compact ? 4 : shortViewport ? 8 : 16);
        FilterBar.Margin = new Thickness(0,0,0,shortViewport ? 8 : 14);
        ListFooter.Margin = new Thickness(0,shortViewport ? 8 : 12,0,0);
    }
    private async void MoveCategory_Click(object sender,RoutedEventArgs e)
    {
        if(_model is null)return;
        if(!await _model.ResolvePendingChangesAsync())return;
        var ids=MediaList.SelectedItems.Cast<Sinalo.Domain.LibraryMedia>().Select(x=>x.Id).ToArray();
        if(ids.Length==0 && _model.Selected is { } item)ids=[item.Id];
        if(ids.Length>0 && UiDialog.Confirm(Window.GetWindow(this),"Transferir mídias","Alterar a categoria preserva arquivos, histórico e vínculos internos.","Transferir"))await _model.MoveSelectedAsync(ids);
    }
    private async void MoveExisting_Click(object sender,RoutedEventArgs e)
    {
        if(_model is {ExistingImportIds.Count:>0} && UiDialog.Confirm(Window.GetWindow(this),"Mover cadastros existentes","Transferir os itens já cadastrados para a categoria escolhida? Nenhum arquivo será duplicado.","Transferir"))await _model.MoveExistingImportsAsync();
    }
    private async void CategoryLink_Click(object sender,RoutedEventArgs e)
    {
        if(_model is not null && await _model.ResolvePendingChangesAsync())(Window.GetWindow(this) as MainWindow)?.OpenLinkForCategory();
    }
    private async void MediaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selecting || _model is null || _model.IsRefreshing || e.AddedItems.Count == 0) return;
        _selecting = true;
        try
        {
            await _model.SelectVideoAsync(e.AddedItems[0] as Sinalo.Domain.LibraryMedia);
            if(MediaList.SelectedItems.Count<=1)MediaList.SelectedItem=_model.Selected;
        }
        finally { _selecting = false; }
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private void Queue_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.ShowLibraryQueue();
    private async void ClosePresentation_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main && main.PresentationOutputService is not null)
        { await main.PresentationOutputService.CloseAsync(); main.RefreshLibraryContext(); }
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel model || model.IsImporting) return;
        var picker = new OpenFileDialog { Filter = "Vídeos MP4|*.mp4", Multiselect = true, CheckFileExists = true };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) await model.ImportAsync(picker.FileNames);
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel model || model.IsImporting) return;
        var picker = new OpenFolderDialog { Title = "Pasta com vídeos MP4" };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) await model.ImportAsync([picker.FolderName]);
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void Locate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel { HasSelection: true } model) return;
        var picker = new OpenFileDialog { Filter = "Vídeos MP4|*.mp4", CheckFileExists = true };
        if (picker.ShowDialog(Window.GetWindow(this)) == true && UiDialog.Confirm(Window.GetWindow(this), "Localizar arquivo", "Validar e usar este arquivo para o cadastro selecionado?", "Validar arquivo"))
            await model.LocateAsync(picker.FileName);
    }
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel { Selected: { } selected } model) return;
        if (!await model.ResolvePendingChangesAsync()) return;
        var message = selected.IsImported
            ? model.DeleteCopy ? "Remover o cadastro e excluir a cópia do Sinalo? O original será preservado." : "Remover somente o cadastro? A cópia e o original ficarão no disco."
            : "Excluir o vídeo baixado? Ele poderá ser baixado novamente pela configuração do programa.";
        if (UiDialog.Confirm(Window.GetWindow(this), "Remover vídeo", message, model.RemoveLabel)) await model.RemoveAsync();
    }
}
