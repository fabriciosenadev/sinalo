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
        DataContext = ReleaseNotesLoader.Load();
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, _themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());
    }
}
