using System.Windows;
using System.Diagnostics.CodeAnalysis;
using Sinalo.Application.Synchronization;
using Button = System.Windows.Controls.Button;
using DockPanel = System.Windows.Controls.DockPanel;
using Orientation = System.Windows.Controls.Orientation;
using ScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Sinalo.App;

[ExcludeFromCodeCoverage]
public sealed class SynchronizationDiagnosticWindow : Window
{
    public SynchronizationDiagnosticWindow(SynchronizationDiagnostic diagnostic, SystemThemeService? themeService)
    {
        Title = "Detalhes da sincronização";
        Width = 580;
        Height = 470;
        MinWidth = 460;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["Brush.Window"];
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());

        var copy = new Button { Content = "Copiar detalhes para suporte", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 8, 0) };
        copy.Click += (_, _) =>
        {
            System.Windows.Clipboard.SetText(diagnostic.SupportText);
            copy.Content = "Detalhes copiados";
        };
        var close = new Button { Content = "Fechar", IsCancel = true, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0) };
        var text = new TextBox
        {
            Text = $"{diagnostic.FriendlyMessage}\n\nPrograma: {diagnostic.SourceName}\nEtapa: {SynchronizationFailureClassifier.GetStageLabel(diagnostic.Stage)}\n\nO que fazer\n{diagnostic.RecommendedAction}\n\nOcorrido em: {diagnostic.OccurredAt.LocalDateTime:dd/MM/yyyy HH:mm}",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12)
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(copy);
        buttons.Children.Add(close);
        var panel = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(text);
        Content = panel;
    }
}
