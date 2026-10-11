using System.Windows;
using System.Windows.Controls;
namespace Sinalo.App.Views;
public partial class WorshipTimerView : System.Windows.Controls.UserControl
{
 public WorshipTimerView()
 {
     InitializeComponent();
     SizeChanged += (_,_) =>
     {
         var wide = ActualWidth >= 1000;
         AudioColumn.Width = new GridLength(wide ? 1 : 0, wide ? GridUnitType.Star : GridUnitType.Pixel);
         Grid.SetColumn(AudioPanel, wide ? 1 : 0);
         Grid.SetRow(AudioPanel, wide ? 0 : 1);
         AudioPanel.Margin = wide ? new Thickness(16,0,0,0) : new Thickness(0,16,0,0);
     };
 }
 private void WorshipTimerStartStop_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerStartStop_Click(sender, e);
 private void WorshipTimerAdjust_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerAdjust_Click(sender, e);
 private void WorshipTimerConfiguration_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerConfiguration_SelectionChanged(sender, e);
 private void WorshipTimerConfiguration_Changed(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerConfiguration_Changed(sender, e);
 private void WorshipTimerAudioConfiguration_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerAudioConfiguration_SelectionChanged(sender, e);
 private void WorshipTimerAudioSeek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerAudioSeek_ValueChanged(sender, e);
 private void PlayWorshipTimerAudio_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.PlayWorshipTimerAudio_Click(sender, e);
 private void PauseResumeWorshipTimerAudio_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.PauseResumeWorshipTimerAudio_Click(sender, e);
 private void StopWorshipTimerAudio_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.StopWorshipTimerAudio_Click(sender, e);
 private void WorshipTimerAudioConfiguration_Changed(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.WorshipTimerAudioConfiguration_Changed(sender, e);
 private void OpenWorshipTimerPresentation_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenWorshipTimerPresentation_Click(sender, e);
 private void CloseWorshipTimerPresentation_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.CloseWorshipTimerPresentation_Click(sender, e);
}
