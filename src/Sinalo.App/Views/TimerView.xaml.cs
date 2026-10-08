using System.Windows;
using System.Windows.Controls;
namespace Sinalo.App.Views;
public partial class TimerView : System.Windows.Controls.UserControl
{
 public TimerView() { InitializeComponent(); }
 private void OpenTimerPresentation_Click(object sender,RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenTimerPresentation_Click(sender,e);
 private void CloseTimerPresentation_Click(object sender,RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.CloseTimerPresentation_Click(sender,e);
 private void TimerStartPause_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.TimerStartPause_Click(sender, e);
 private void TimerReset_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.TimerReset_Click(sender, e);
 private void TimerConfiguration_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.TimerConfiguration_SelectionChanged(sender, e);
 private void TimerConfiguration_Changed(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.TimerConfiguration_Changed(sender, e);
}
