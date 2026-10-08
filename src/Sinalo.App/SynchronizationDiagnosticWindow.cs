using System.Windows;
using System.Windows.Controls;
using Sinalo.Application.Synchronization;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
namespace Sinalo.App;

public sealed class SynchronizationDiagnosticWindow : Window
{
    public SynchronizationDiagnosticWindow(SynchronizationDiagnostic diagnostic, SystemThemeService? themeService)
    {
        Title="Detalhes do download";Width=640;Height=520;MinWidth=460;MinHeight=360;
        SetResourceReference(StyleProperty,typeof(Window));
        WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(BackgroundProperty,"Brush.Window");
        SourceInitialized+=(_,_)=>SystemThemeService.ApplyTitleBar(this,themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());
        Loaded+=(_,_)=>MaxHeight=SystemParameters.WorkArea.Height;
        var root=new Grid{Margin=new Thickness(24)};root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        root.Children.Add(new TextBlock{Text="Não foi possível concluir a operação",FontSize=24,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
        var body=new StackPanel();
        body.Children.Add(new TextBlock{Text=diagnostic.FriendlyMessage,FontSize=16,TextWrapping=TextWrapping.Wrap});
        body.Children.Add(new TextBlock{Text=$"Programa: {diagnostic.SourceName}\nEtapa: {SynchronizationFailureClassifier.GetStageLabel(diagnostic.Stage)}",Margin=new Thickness(0,12,0,16)});
        body.Children.Add(new TextBlock{Text="O que fazer",FontSize=18,FontWeight=FontWeights.SemiBold});
        body.Children.Add(new TextBlock{Text=diagnostic.RecommendedAction,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,16)});
        body.Children.Add(new TextBlock{Text=$"Ocorrido em {diagnostic.OccurredAt.LocalDateTime:dd/MM/yyyy HH:mm}"});
        var details=new Expander{Header="Detalhes para suporte",Margin=new Thickness(0,16,0,0),Content=new TextBox{Text=diagnostic.SupportText,IsReadOnly=true,TextWrapping=TextWrapping.Wrap}};
        body.Children.Add(details);
        var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,12,0)};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        var actions=new WrapPanel{HorizontalAlignment=System.Windows.HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
        var copy=new Button{Content="Copiar detalhes para suporte"};copy.Click+=(_,_)=>{try{System.Windows.Clipboard.SetText(diagnostic.SupportText);copy.Content="Detalhes copiados";}catch(System.Runtime.InteropServices.COMException){copy.Content="Não foi possível copiar. Tente novamente.";}};actions.Children.Add(copy);
        actions.Children.Add(new Button{Content="Fechar",IsCancel=true});Grid.SetRow(actions,2);root.Children.Add(actions);Content=root;
    }
}
