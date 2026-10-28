using System.Windows;
using Sinalo.App.ViewModels;
using Sinalo.Application.Synchronization;

namespace Sinalo.App;
public partial class HealthYouTubeWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly HealthYouTubeViewModel _model;
    public IReadOnlyList<LinkedVideoDownloadRequest> ApprovedRequests { get; private set; } = [];
    public HealthYouTubeWindow(HealthYouTubeViewModel model, SystemThemeService? theme)
    {
        InitializeComponent();
        _model = model; DataContext = model;
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, theme?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());
        Loaded += async (_, _) => { MaxHeight = SystemParameters.WorkArea.Height; MaxWidth = SystemParameters.WorkArea.Width; UiAccessibility.Apply(this); await RefreshVideosAsync(); };
        Closed += (_, _) => _cancellation.Cancel();
    }
    private async Task RefreshVideosAsync()
    {
        await _model.RefreshAsync(_cancellation.Token);
        if (!_cancellation.IsCancellationRequested) VideosScroll.ScrollToTop();
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshVideosAsync();
    private void Approve_Click(object sender, RoutedEventArgs e)
    {
        var approved = _model.CreateApprovedRequests();
        if (approved.Count == 0) return;
        ApprovedRequests = approved; DialogResult = true;
    }
}
