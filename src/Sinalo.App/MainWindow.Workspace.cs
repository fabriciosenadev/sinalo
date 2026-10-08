using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Sinalo.App.ViewModels;
namespace Sinalo.App;

public partial class MainWindow
{
    private string _presentationSceneKind = "conference";
    private HomeViewModel? _uxModel;
    private void InitializeWorkspaceUi()
    {
        DataContextChanged += (_, _) =>
        {
            if (_uxModel is not null) _uxModel.PropertyChanged -= WorkspaceChanged;
            _uxModel = DataContext as HomeViewModel;
            if (_uxModel is not null) _uxModel.PropertyChanged += WorkspaceChanged;
            UpdateWorkspaceLayout();
        };
        SizeChanged += (_, _) => UpdateWorkspaceLayout();
        WorkspaceRegion.SizeChanged += (_,_) => UpdateWorkspaceLayout();
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, area.Width);
            MinHeight = Math.Min(MinHeight, area.Height);
            Width = Math.Min(Width, area.Width);
            Height = Math.Min(Height, area.Height);
            UpdateWorkspaceLayout();
            UiAccessibility.Apply(this);
        };
        Closed += (_, _) => { if (_uxModel is not null) _uxModel.PropertyChanged -= WorkspaceChanged; };
    }
    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeViewModel.ShowCatalogDetails) or nameof(HomeViewModel.IsLibraryWorkspace) or nameof(HomeViewModel.IsPresentationOpen)) UpdateWorkspaceLayout();
    }
    private void UpdateWorkspaceLayout()
    {
        if (CatalogRegion is null || DataContext is not HomeViewModel home) return;
        var wide = WorkspaceRegion.ActualWidth >= 1000;
        home.IsCompactWorkspace = WorkspaceRegion.ActualHeight < 540;
        DetailsColumn.Width = new GridLength(wide && home.ShowCatalogDetails ? 340 : 0);
        Grid.SetColumn(CatalogDetails, wide ? 1 : 0);
        Grid.SetColumnSpan(CatalogDetails, wide ? 1 : 2);
        CatalogDetails.Margin = wide ? new Thickness(16,0,0,0) : new Thickness(0);
        CatalogListRegion.Visibility = !wide && home.ShowCatalogDetails ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ProgramOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
    private void ClearSearch_Click(object sender, RoutedEventArgs e) { if (DataContext is HomeViewModel home) home.SearchQuery = ""; }
    private void CloseDetails_Click(object sender, RoutedEventArgs e) { if (DataContext is HomeViewModel home) home.DetailsOpen = false; }
    private void AddLinkForProgram_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;
        var source = home.Sources.FirstOrDefault(item => item.Name == home.SelectedSource)?.Source;
        home.SelectLinkedVideoWorkspace();
        if (source is not null) home.SelectedLinkedVideoDestination = home.LinkedVideoDestinations.FirstOrDefault(item => item.Source == source);
    }
    private void OpenDownloads_Click(object sender, RoutedEventArgs e) => new DownloadsWindow(this).ShowDialog();
    internal void ShowDownloads() => new DownloadsWindow(this).ShowDialog();
    private void OpenSchedule_Click(object sender, RoutedEventArgs e) => new SessionScheduleWindow(this).ShowDialog();
    private void OpenUpdate_Click(object sender, RoutedEventArgs e) => new UpdateStatusWindow(this).ShowDialog();
    internal async void OpenRafflePresentation_Click(object sender,RoutedEventArgs e)
    {
        if(DataContext is not HomeViewModel home || MonitorService is null || PresentationOutputService is null || home.SelectedPlaybackScreen is null) return;
        var output=Sinalo.Application.Monitors.OutputSelectionResolver.Resolve(new Sinalo.Application.Playback.PlaybackConfiguration(home.SelectedPlaybackScreen.ScreenNumber, home.SelectedPlaybackScreen.MonitorKey),await MonitorService.GetOutputsAsync());
        if(output is null){home.OperationMessage="A tela selecionada não está disponível.";return;}
        var result=await PresentationOutputService.ShowAsync(new Sinalo.Application.Presentation.PresentationScene("Sorteio",home.Raffle.CurrentWinner,home.Raffle.StatusLabel),output);
        _presentationSceneKind="raffle";home.IsPresentationOpen=result.Succeeded;home.OperationMessage=result.Message;
    }
    private void PlaySelected_Click(object sender, RoutedEventArgs e) => CatalogItem_DoubleClick(sender, new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left));
}
