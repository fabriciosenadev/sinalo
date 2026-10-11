using System.Windows;
using System.Windows.Controls;
namespace Sinalo.App.Views;
public partial class LinkedVideoView : System.Windows.Controls.UserControl
{
 public LinkedVideoView() { InitializeComponent(); }
 private void OpenDownloads_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.ShowDownloads();
 private void InspectLinkedVideo_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.InspectLinkedVideo_Click(sender, e);
 private void QueueLinkedVideo_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.QueueLinkedVideo_Click(sender, e);
}
