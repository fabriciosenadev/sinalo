using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using Sinalo.Domain;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Sinalo.App;

/// <summary>Diálogo temporário: descobrir não altera o catálogo offline; só a confirmação cria a solicitação da fila.</summary>
[ExcludeFromCodeCoverage]
public sealed class ManualVideoSelectionWindow : Window
{
    private readonly List<ManualVideoSelectionItem> _items;
    private readonly TextBlock _summary = new();
    private readonly Button _download = new();

    public IReadOnlyList<string> SelectedItemIds => _items.Where(item => item.IsSelected && item.CanDownload).Select(item => item.Item.Id).ToArray();

    public ManualVideoSelectionWindow(string sourceName, IReadOnlyList<ManualVideoSelectionItem> items, SystemThemeService? themeService)
    {
        _items = items.ToList();
        Title = "Selecionar vídeos para baixar";
        Width = 760;
        Height = 620;
        MinWidth = 560;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("Brush.Window");
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(Text("Selecionar vídeos para baixar", 24, FontWeights.SemiBold, "Brush.TextPrimary"));
        heading.Children.Add(Text(sourceName, 13, FontWeights.Normal, "Brush.TextSecondary", new Thickness(0, 4, 0, 2)));
        heading.Children.Add(Text("Marque somente os vídeos que deseja baixar. Itens já disponíveis offline não serão baixados novamente.", 12, FontWeights.Normal, "Brush.TextSecondary", new Thickness(0, 0, 0, 16)));
        root.Children.Add(heading);

        var list = new StackPanel();
        foreach (var item in _items) list.Children.Add(CreateRow(item));
        if (_items.Count == 0) list.Children.Add(Text("Nenhum vídeo publicado foi encontrado para este programa.", 14, FontWeights.Normal, "Brush.TextSecondary", new Thickness(8)));
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 10, 0) };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        _summary.Foreground = Brush("Brush.TextSecondary");
        _summary.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_summary, Dock.Left);
        footer.Children.Add(_summary);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0) };
        _download.Content = "Baixar selecionados";
        _download.IsDefault = true;
        _download.Padding = new Thickness(16, 8, 16, 8);
        _download.Margin = new Thickness(8, 0, 0, 0);
        _download.Click += (_, _) => DialogResult = true;
        _download.SetResourceReference(FrameworkElement.StyleProperty, "Button.Primary");
        actions.Children.Add(cancel);
        actions.Children.Add(_download);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;
        UpdateSummary();
    }

    private Border CreateRow(ManualVideoSelectionItem selection)
    {
        var border = new Border { Background = Brush("Brush.SurfaceRaised"), BorderBrush = Brush("Brush.Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8) };
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var check = new CheckBox { IsChecked = selection.IsSelected, IsEnabled = selection.CanDownload, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) };
        check.Checked += (_, _) => { selection.IsSelected = true; UpdateSummary(); };
        check.Unchecked += (_, _) => { selection.IsSelected = false; UpdateSummary(); };
        panel.Children.Add(check);
        var details = new StackPanel();
        details.Children.Add(Text(selection.Item.Title, 14, FontWeights.SemiBold, "Brush.TextPrimary"));
        details.Children.Add(Text($"{selection.Item.ScheduledDate:dd/MM/yyyy}  •  {selection.Status}", 12, FontWeights.Normal, selection.CanDownload ? "Brush.TextSecondary" : "Brush.TextSecondary", new Thickness(0, 3, 0, 0)));
        details.Children.Add(Text(selection.SizeLabel, 11, FontWeights.Normal, "Brush.TextSecondary", new Thickness(0, 3, 0, 0)));
        Grid.SetColumn(details, 1);
        panel.Children.Add(details);
        border.Child = panel;
        return border;
    }

    private void UpdateSummary()
    {
        var selected = _items.Where(item => item.IsSelected && item.CanDownload).ToArray();
        var bytes = selected.Sum(item => item.Item.Assets.FirstOrDefault()?.ExpectedSizeBytes ?? 0);
        var unknown = selected.Count(item => item.Item.Assets.FirstOrDefault()?.ExpectedSizeBytes is null);
        _summary.Text = selected.Length == 0
            ? "Nenhum vídeo selecionado"
            : $"{selected.Length} vídeo(s) selecionado(s) • {FormatBytes(bytes)}{(unknown > 0 ? " + tamanhos não informados" : string.Empty)}";
        _download.IsEnabled = selected.Length > 0;
    }

    private Brush Brush(string key) => (Brush)System.Windows.Application.Current.Resources[key];
    private TextBlock Text(string value, double size, FontWeight weight, string brush, Thickness? margin = null) => new() { Text = value, FontSize = size, FontWeight = weight, Foreground = Brush(brush), TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness() };
    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024 / 1024:0.0} GB" : $"{bytes / 1024d / 1024:0} MB";
}

[ExcludeFromCodeCoverage]
public sealed class ManualVideoSelectionItem(ContentItem item, bool isSelected)
{
    public ContentItem Item { get; } = item;
    public bool IsSelected { get; set; } = isSelected;
    public bool IsOffline => Item.IsReadyOffline && !string.IsNullOrWhiteSpace(Item.LocalPath) && File.Exists(Item.LocalPath);
    public bool HasOfficialFile => Item.Assets.Count > 0;
    public bool CanDownload => HasOfficialFile && !IsOffline;
    public string Status => IsOffline ? "Já disponível offline" : HasOfficialFile ? "Disponível para baixar" : "Arquivo oficial não identificado";
    public string SizeLabel => Item.Assets.FirstOrDefault()?.ExpectedSizeBytes is { } bytes ? $"Tamanho estimado: {FormatBytes(bytes)}" : HasOfficialFile ? "Tamanho será confirmado antes do download" : "Não disponível para download";
    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024 / 1024:0.0} GB" : $"{bytes / 1024d / 1024:0} MB";
}
