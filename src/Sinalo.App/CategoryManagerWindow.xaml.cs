using System.Windows;
using Sinalo.Application.Library;
using Sinalo.App.ViewModels;

namespace Sinalo.App;

public partial class CategoryManagerWindow : Window
{
    private readonly CategoryManagerViewModel _model;
    public CategoryManagerWindow(IMediaCategoryService service, string? initialCategoryId = null)
    {
        InitializeComponent();DataContext=_model=new(service, initialCategoryId);
        Loaded+=async(_,_)=>{MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;await _model.RefreshAsync();};
        Closing+=(_,e)=>{if(_model.Busy)e.Cancel=true;};
    }
    private async void Delete_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            var request=await _model.PrepareRemovalAsync();
            if(request is null)return;
            var dialog=new CategoryRemovalWindow(request){Owner=this};
            if(dialog.ShowDialog()!=true){_model.CancelRemoval();return;}
            if(request.IsDelete && (!request.IsEmpty || request.Category.IsDeleting) && !UiDialog.Confirm(this,"Exclusão definitiva",
                request.Summary+"\nAs cópias gerenciadas, inclusive as fixadas, e os vínculos internos serão removidos. Originais externos serão preservados. Esta operação não pode ser desfeita.","Excluir definitivamente"))
            {_model.CancelRemoval();return;}
            await _model.CompleteRemovalAsync(request);
        }
        catch(Exception ex){_model.CancelRemoval();_model.Message=CategoryManagerViewModel.Describe(ex);}
    }
}
