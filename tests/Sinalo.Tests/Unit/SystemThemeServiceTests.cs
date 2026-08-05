using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Sinalo.Tests.Unit;

public sealed class SystemThemeServiceTests
{
    [Fact]
    public void Palette_ShouldExposeEquivalentLightAndDarkWindowColors()
    {
        Assert.Equal("#F4F7FA", Sinalo.App.SystemThemeService.GetPalette(false)["Brush.Window"]);
        Assert.Equal("#0F172A", Sinalo.App.SystemThemeService.GetPalette(true)["Brush.Window"]);
        Assert.Contains("Brush.Accent", Sinalo.App.SystemThemeService.GetPalette(false).Keys);
        Assert.Contains("Brush.Accent", Sinalo.App.SystemThemeService.GetPalette(true).Keys);
    }

    [Fact]
    public void ApplyToResources_ShouldReplaceColorsForBothModes()
    {
        var resources = new ResourceDictionary();
        Sinalo.App.SystemThemeService.ApplyToResources(resources, false);
        Assert.Equal("#FFF4F7FA", ((SolidColorBrush)resources["Brush.Window"]).Color.ToString());

        Sinalo.App.SystemThemeService.ApplyToResources(resources, true);
        Assert.Equal("#FF0F172A", ((SolidColorBrush)resources["Brush.Window"]).Color.ToString());
        Assert.Equal("#FF2DD4BF", ((SolidColorBrush)resources["Brush.Accent"]).Color.ToString());
    }

    [Theory]
    [InlineData(UserPreferenceCategory.General, true)]
    [InlineData(UserPreferenceCategory.Color, true)]
    [InlineData(UserPreferenceCategory.Keyboard, false)]
    public void PreferenceCategory_ShouldRefreshOnlyForThemeRelevantChanges(UserPreferenceCategory category, bool expected)
    {
        Assert.Equal(expected, Sinalo.App.SystemThemeService.ShouldRefreshFor(category));
    }
}
