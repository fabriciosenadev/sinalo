using System.Windows;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
namespace Sinalo.App;
public partial class UpdateStatusWindow : Window
{
 private readonly MainWindow _main;
 public UpdateStatusWindow(MainWindow main){_main=main;Owner=main;InitializeComponent();SetBinding(DataContextProperty,new Binding("DataContext"){Source=main});Loaded+=(_,_)=>MaxHeight=SystemParameters.WorkArea.Height;}
 private void Install_Click(object sender,RoutedEventArgs e){if(UiDialog.Confirm(this,"Atualizar e reiniciar?","O Sinalo encerrará reprodução, áudio, apresentação e downloads antes de instalar. Deseja continuar?","Atualizar")){_main.InstallUpdate_Click(sender,e);}}
}
