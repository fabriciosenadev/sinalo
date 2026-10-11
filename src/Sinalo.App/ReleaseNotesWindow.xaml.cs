using System.Windows;
using Sinalo.App.ReleaseNotes;

namespace Sinalo.App;

public partial class ReleaseNotesWindow : Window
{
    private readonly SystemThemeService? _themeService;

    public ReleaseNotesWindow(SystemThemeService? themeService)
    {
        _themeService = themeService;
        InitializeComponent();
        DataContext = new ReleaseNotesViewModel(ReleaseNotesLoader.Load(), typeof(ReleaseNotesWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
        Loaded += (_,_) => MaxHeight = SystemParameters.WorkArea.Height;
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, _themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());
    }
}
