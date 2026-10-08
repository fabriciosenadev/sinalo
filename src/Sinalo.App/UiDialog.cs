using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
namespace Sinalo.App;

/// <summary>Diálogos operacionais com ação segura e identidade comum.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class UiDialog
{
    public enum UnsavedChoice { Cancel, Save, Discard }
    public static UnsavedChoice ChooseUnsavedChanges(Window owner)
    {
        var window = Create(owner, "Alterações não salvas");
        var result = UnsavedChoice.Cancel;
        var grid = new Grid { Margin = new Thickness(24) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = "Salvar as alterações?", FontSize = 22, FontWeight = FontWeights.SemiBold });
        var body = new TextBlock { Text = "Escolha salvar, descartar o que foi alterado ou continuar editando.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,16,0,16) };
        Grid.SetRow(body,1); grid.Children.Add(body);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(new System.Windows.Controls.Button { Content = "Continuar editando", IsCancel = true, IsDefault = true });
        foreach (var choice in new[] { UnsavedChoice.Discard, UnsavedChoice.Save })
        {
            var button = new System.Windows.Controls.Button { Content = choice == UnsavedChoice.Save ? "Salvar" : "Descartar" };
            button.Click += (_,_) => { result = choice; window.DialogResult = true; };
            actions.Children.Add(button);
        }
        Grid.SetRow(actions,2); grid.Children.Add(actions); window.Content = grid;
        window.ShowDialog(); return result;
    }
    public static void Inform(Window owner, string title, string message)
    {
        var window = Create(owner, title);
        var panel = new DockPanel { Margin = new Thickness(24) };
        var close = new System.Windows.Controls.Button { Content = "Entendi", IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        var heading = new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) };
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        panel.Children.Add(new ScrollViewer { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        window.Content = panel; window.ShowDialog();
    }
    public static bool Confirm(Window owner, string title, string message, string action = "Confirmar")
    {
        var window = Create(owner, title);
        var grid = new Grid { Margin = new Thickness(24) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold });
        var body = new ScrollViewer { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0,16,0,16), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(body,1); grid.Children.Add(body);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new System.Windows.Controls.Button { Content = "Cancelar", IsCancel = true, IsDefault = true };
        var confirm = new System.Windows.Controls.Button { Content = action };
        confirm.Click += (_, _) => window.DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(confirm);
        Grid.SetRow(actions,2); grid.Children.Add(actions);
        window.Content = grid;
        return window.ShowDialog() == true;
    }
    public static Window Create(Window owner, string title)
    {
        var window = new Window { Owner = owner, Title = title, Width = 600, Height = 440, MinWidth = 400, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.SetResourceReference(Window.BackgroundProperty, "Brush.Window");
        window.SetResourceReference(Window.ForegroundProperty, "Brush.TextPrimary");
        window.Loaded += (_, _) => { window.MaxHeight = SystemParameters.WorkArea.Height; window.MaxWidth = SystemParameters.WorkArea.Width; };
        return window;
    }
}
