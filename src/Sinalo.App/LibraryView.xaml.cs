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
    private LibraryEditDecision ConfirmChanges() => MessageBox.Show("Salvar as alterações deste vídeo antes de continuar?\nSim: salvar. Não: descartar. Cancelar: continuar editando.", "Alterações não salvas", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
    { MessageBoxResult.Yes => LibraryEditDecision.Save, MessageBoxResult.No => LibraryEditDecision.Discard, _ => LibraryEditDecision.Cancel };
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
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
        LibraryHeader.Margin = new Thickness(0,0,0,shortViewport ? 8 : 14);
        TabBar.Margin = new Thickness(0,0,0,shortViewport ? 8 : 16);
        FilterBar.Margin = new Thickness(0,0,0,shortViewport ? 8 : 14);
        ListFooter.Margin = new Thickness(0,shortViewport ? 8 : 12,0,0);
    }
    private async void MediaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selecting || _model is null || _model.IsRefreshing || e.AddedItems.Count == 0) return;
        _selecting = true;
        try
        {
            await _model.SelectVideoAsync(e.AddedItems[0] as Sinalo.Domain.LibraryMedia);
            BindingOperations.GetBindingExpression(MediaList, System.Windows.Controls.ListBox.SelectedItemProperty)?.UpdateTarget();
        }
        finally { _selecting = false; }
    }
    private void OutputScreen_SelectionChanged(object sender, SelectionChangedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.LibraryOutputScreenChanged(sender,e);
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
        if (picker.ShowDialog(Window.GetWindow(this)) == true && MessageBox.Show("Validar e usar este arquivo para o cadastro selecionado?", "Localizar arquivo", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
        if (MessageBox.Show(message, "Sinalo", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await model.RemoveAsync();
    }
}
