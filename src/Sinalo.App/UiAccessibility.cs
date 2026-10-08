using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using Panel = System.Windows.Controls.Panel;
namespace Sinalo.App;

/// <summary>Associa controles de formulário aos rótulos visuais adjacentes.</summary>
public static class UiAccessibility
{
    public static void Apply(DependencyObject root)
    {
        if(root is FrameworkElement element && (element is System.Windows.Controls.TextBox || element is System.Windows.Controls.ComboBox || element is Slider))
        {
            if(string.IsNullOrWhiteSpace(AutomationProperties.GetName(element)))
            {
                var parent=VisualTreeHelper.GetParent(element);
                if(parent is Panel panel)
                {
                    var index=panel.Children.IndexOf(element);
                    var label=panel.Children.Cast<UIElement>().Take(Math.Max(0,index)).OfType<TextBlock>().LastOrDefault();
                    if(label is not null) AutomationProperties.SetName(element,label.Text);
                }
                if(string.IsNullOrWhiteSpace(AutomationProperties.GetName(element)) && element.ToolTip is string help) AutomationProperties.SetName(element,help);
            }
        }
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)Apply(VisualTreeHelper.GetChild(root,i));
    }
}
