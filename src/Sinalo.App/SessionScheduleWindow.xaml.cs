using System.Windows;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
namespace Sinalo.App;
public partial class SessionScheduleWindow : Window
{
 private readonly MainWindow _main;
 public SessionScheduleWindow(MainWindow main){_main=main;Owner=main;InitializeComponent();SetBinding(DataContextProperty,new Binding("DataContext"){Source=main});Loaded+=(_,_)=>MaxHeight=SystemParameters.WorkArea.Height;}
 private void Up_Click(object sender,RoutedEventArgs e)=>_main.MoveScheduleUp_Click(sender,e);
 private void Down_Click(object sender,RoutedEventArgs e)=>_main.MoveScheduleDown_Click(sender,e);
 private void Remove_Click(object sender,RoutedEventArgs e)=>_main.RemoveSchedule_Click(sender,e);
}
