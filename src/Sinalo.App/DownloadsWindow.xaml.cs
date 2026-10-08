using System.Windows;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
namespace Sinalo.App;
public partial class DownloadsWindow : Window
{
 private readonly MainWindow _main;
 public DownloadsWindow(MainWindow main) { _main=main; Owner=main; InitializeComponent(); SetBinding(DataContextProperty,new Binding("DataContext"){Source=main}); Loaded += (_,_) => MaxHeight=SystemParameters.WorkArea.Height; }
 private void Cancel_Click(object sender,RoutedEventArgs e) { if(UiDialog.Confirm(this,"Cancelar todos os downloads?","O pedido ativo e todos os pendentes serão cancelados. Arquivos já concluídos serão preservados.","Cancelar downloads")) _main.CancelSynchronizationQueue_Click(sender,e); }
 private void Diagnostic_Click(object sender,RoutedEventArgs e) => _main.ViewSynchronizationDiagnostic_Click(sender,e);
}
