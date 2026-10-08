using System.Windows;
using System.Windows.Controls;
namespace Sinalo.App.Views;
public partial class RaffleView : System.Windows.Controls.UserControl
{
 public RaffleView() { InitializeComponent(); }
 private void OpenRafflePresentation_Click(object sender,RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenRafflePresentation_Click(sender,e);
 private void RaffleAction_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.RaffleAction_Click(sender, e);
}
