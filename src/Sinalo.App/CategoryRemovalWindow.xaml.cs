using System.Windows;
using Sinalo.App.ViewModels;

namespace Sinalo.App;

public partial class CategoryRemovalWindow : Window
{
    public CategoryRemovalWindow(CategoryRemovalViewModel model)
    {
        InitializeComponent(); DataContext=model;
        if(model.IsEmpty && !model.Category.IsDeleting)Height=340;
        Loaded+=(_,_)=>{MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;};
    }
    private void Confirm_Click(object sender,RoutedEventArgs e)
    {
        if(DataContext is CategoryRemovalViewModel {CanContinue:true})DialogResult=true;
    }
}
