using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Sinalo.Domain;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
namespace Sinalo.App;

public sealed class ManualVideoSelectionWindow : Window
{
    private readonly List<ManualVideoSelectionItem> _items;
    private readonly TextBlock _summary = new();
    private readonly Button _download = new();
    private readonly ListBox _list = new();
    public IReadOnlyList<string> SelectedItemIds => _items.Where(item=>item.IsSelected && item.CanDownload).Select(item=>item.Item.Id).ToArray();

    public ManualVideoSelectionWindow(string sourceName, IReadOnlyList<ManualVideoSelectionItem> items, SystemThemeService? themeService)
    {
        _items=items.ToList(); Title="Escolher vídeos para baixar";Width=780;Height=620;MinWidth=520;MinHeight=400;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty,typeof(Window));
        SetResourceReference(BackgroundProperty,"Brush.Window");
        SourceInitialized+=(_,_)=>SystemThemeService.ApplyTitleBar(this,themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());
        Loaded+=(_,_)=>MaxHeight=SystemParameters.WorkArea.Height;
        var grid=new Grid{Margin=new Thickness(24)};
        grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var header=new StackPanel();
        header.Children.Add(new TextBlock{Text=Title,FontSize=24,FontWeight=FontWeights.SemiBold});
        header.Children.Add(new TextBlock{Text=sourceName,Margin=new Thickness(0,4,0,12)});
        header.Children.Add(new TextBlock{Text="Selecione arquivos disponíveis. Vídeos já locais não serão baixados novamente.",TextWrapping=TextWrapping.Wrap});
        var search=new TextBox{Margin=new Thickness(0,12,0,12),ToolTip="Pesquisar por título ou data"};
        System.Windows.Automation.AutomationProperties.SetName(search,"Pesquisar vídeos por título ou data");
        header.Children.Add(new TextBlock{Text="Pesquisar por título ou data",Margin=new Thickness(0,12,0,0)});
        header.Children.Add(search);
        search.TextChanged+=(_,_)=>_list.ItemsSource=_items.Where(item=>string.IsNullOrWhiteSpace(search.Text) || item.Item.Title.Contains(search.Text,StringComparison.OrdinalIgnoreCase) || item.Item.ScheduledDate.ToString("dd/MM/yyyy").Contains(search.Text));
        var choices=new WrapPanel();
        var all=new Button{Content="Selecionar disponíveis"};all.Click+=(_,_)=>{foreach(var item in _items.Where(item=>item.CanDownload))item.IsSelected=true;};
        var clear=new Button{Content="Limpar seleção"};clear.Click+=(_,_)=>{foreach(var item in _items)item.IsSelected=false;};
        choices.Children.Add(all);choices.Children.Add(clear);header.Children.Add(choices);grid.Children.Add(header);
        _list.ItemsSource=_items;_list.HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch;
        _list.SetResourceReference(BackgroundProperty,"Brush.Surface");
        _list.SetResourceReference(BorderBrushProperty,"Brush.Border");
        ScrollViewer.SetHorizontalScrollBarVisibility(_list,ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(_list,true);VirtualizingPanel.SetVirtualizationMode(_list,VirtualizationMode.Recycling);
        _list.ItemTemplate=(DataTemplate)XamlReader.Parse("""
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Border Padding="12" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><CheckBox IsChecked="{Binding IsSelected, Mode=TwoWay}" IsEnabled="{Binding CanDownload}" AutomationProperties.Name="{Binding Item.Title}" Margin="0,0,12,0"/><StackPanel Grid.Column="1"><TextBlock Text="{Binding Item.Title}" FontSize="16" FontWeight="SemiBold" TextWrapping="Wrap"/><TextBlock Text="{Binding Item.ScheduledDate, StringFormat=dd/MM/yyyy}" Margin="0,6,0,0"/><TextBlock Text="{Binding Status}"/><TextBlock Text="{Binding SizeLabel}" Foreground="{DynamicResource Brush.TextSecondary}"/></StackPanel></Grid></Border></DataTemplate>
""");
        Grid.SetRow(_list,1);grid.Children.Add(_list);
        var footer=new StackPanel{Margin=new Thickness(0,16,0,0)};
        _summary.TextWrapping=TextWrapping.Wrap;footer.Children.Add(_summary);
        var actions=new WrapPanel{HorizontalAlignment=System.Windows.HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        actions.Children.Add(new Button{Content="Cancelar",IsCancel=true});
        _download.Content="Adicionar selecionados à fila";_download.IsDefault=true;
        _download.SetResourceReference(StyleProperty,"Button.Primary");_download.Click+=(_,_)=>DialogResult=true;actions.Children.Add(_download);footer.Children.Add(actions);
        Grid.SetRow(footer,2);grid.Children.Add(footer);Content=grid;
        foreach(var item in _items)item.PropertyChanged+=(_,_)=>UpdateSummary();
        UpdateSummary();
    }
    private void UpdateSummary()
    {
        var selected=_items.Where(item=>item.IsSelected && item.CanDownload).ToArray();
        var size=selected.Sum(item=>item.Item.Assets.FirstOrDefault()?.ExpectedSizeBytes ?? 0);
        var unknown=selected.Any(item=>item.Item.Assets.FirstOrDefault()?.ExpectedSizeBytes is null);
        _summary.Text=_items.Count==0?"Nenhum vídeo publicado foi encontrado.":selected.Length==0?"Nenhum vídeo selecionado":$"{selected.Length} vídeo(s) · {size/1024d/1024:0} MB conhecidos{(unknown?" + tamanhos não informados":"")}";
        _download.IsEnabled=selected.Length>0;
    }
}

public sealed class ManualVideoSelectionItem(ContentItem item, bool isSelected) : INotifyPropertyChanged
{
    private bool _isSelected=isSelected;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ContentItem Item {get;}=item;
    public bool IsSelected {get=>_isSelected;set{if(_isSelected==value)return;_isSelected=value;PropertyChanged?.Invoke(this,new(nameof(IsSelected)));}}
    public bool IsOffline=>Item.IsReadyOffline && !string.IsNullOrWhiteSpace(Item.LocalPath) && File.Exists(Item.LocalPath);
    public bool HasOfficialFile=>Item.Assets.Count>0;
    public bool CanDownload=>HasOfficialFile && !IsOffline;
    public string Status=>IsOffline?"Já disponível offline":HasOfficialFile?"Disponível para baixar":"Arquivo oficial não identificado";
    public string SizeLabel=>Item.Assets.FirstOrDefault()?.ExpectedSizeBytes is {} bytes?$"Tamanho estimado: {bytes/1024d/1024:0} MB":HasOfficialFile?"Tamanho será confirmado antes do download":"Não disponível para download";
}
