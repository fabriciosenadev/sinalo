using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Sinalo.App;

public sealed class SystemThemeService(System.Windows.Application application) : IDisposable
{
    private readonly System.Windows.Application _application = application;

    public void Start()
    {
        ApplyCurrentTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void ApplyCurrentTheme() => ApplyTheme(IsWindowsDarkTheme());

    public void ApplyTheme(bool isDark)
    {
        ApplyToResources(_application.Resources, isDark);
        foreach (Window window in _application.Windows) ApplyTitleBar(window, isDark);
    }

    public static void ApplyToResources(ResourceDictionary resources, bool isDark)
    {
        var palette = GetPalette(isDark);
        foreach (var (key, color) in palette)
        {
            resources[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)!);
        }
    }

    public static IReadOnlyDictionary<string, string> GetPalette(bool isDark) => isDark ? DarkPalette : LightPalette;

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (!ShouldRefreshFor(e.Category)) return;
        _application.Dispatcher.Invoke(ApplyCurrentTheme);
    }

    public static bool ShouldRefreshFor(UserPreferenceCategory category) =>
        category is UserPreferenceCategory.General or UserPreferenceCategory.Color;

    public static void ApplyTitleBar(Window window, bool isDark)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var value = isDark ? 1 : 0;
        // 20 is the current Windows 11 attribute; 19 keeps compatibility with early Windows 10 builds.
        if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
    }

    public static bool IsWindowsDarkTheme()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private static readonly IReadOnlyDictionary<string, string> LightPalette = new Dictionary<string, string>
    {
        ["Brush.Window"] = "#F4F7FA", ["Brush.Surface"] = "#FFFFFF", ["Brush.SurfaceRaised"] = "#F8FAFC", ["Brush.Border"] = "#DCE4EB",
        ["Brush.TextPrimary"] = "#16324F", ["Brush.TextSecondary"] = "#506273", ["Brush.Accent"] = "#0F766E", ["Brush.AccentStrong"] = "#0F766E", ["Brush.AccentText"] = "#FFFFFF", ["Brush.Focus"] = "#14B8A6",
        ["Brush.Button"] = "#E7EDF3", ["Brush.ButtonBorder"] = "#B9C6D3", ["Brush.ButtonHover"] = "#D5E3EE", ["Brush.ButtonPressed"] = "#C0D5E5", ["Brush.Input"] = "#FFFFFF", ["Brush.Header"] = "#16324F", ["Brush.HeaderBorder"] = "#244B72", ["Brush.HeaderLabel"] = "#C8D8E8", ["Brush.Warning"] = "#9A6B1B", ["Brush.Thumbnail"] = "#E6EEF5", ["Brush.ProgressTrack"] = "#D5E3EE"
    };

    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
    {
        ["Brush.Window"] = "#0F172A", ["Brush.Surface"] = "#172033", ["Brush.SurfaceRaised"] = "#1F2937", ["Brush.Border"] = "#334155",
        ["Brush.TextPrimary"] = "#F8FAFC", ["Brush.TextSecondary"] = "#CBD5E1", ["Brush.Accent"] = "#2DD4BF", ["Brush.AccentStrong"] = "#0F766E", ["Brush.AccentText"] = "#ECFEFF", ["Brush.Focus"] = "#5EEAD4",
        ["Brush.Button"] = "#25344A", ["Brush.ButtonBorder"] = "#475569", ["Brush.ButtonHover"] = "#2D4660", ["Brush.ButtonPressed"] = "#162438", ["Brush.Input"] = "#0F172A", ["Brush.Header"] = "#111C2D", ["Brush.HeaderBorder"] = "#2A405A", ["Brush.HeaderLabel"] = "#94A3B8", ["Brush.Warning"] = "#FBBF24", ["Brush.Thumbnail"] = "#0F172A", ["Brush.ProgressTrack"] = "#334155"
    };
}
