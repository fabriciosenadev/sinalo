using System.Windows;
using System.Windows.Controls;
namespace Sinalo.App;
public partial class SettingsWindow
{
    private bool _saving;
    private string? _initialSettings;
    private bool _syncingModes;
    public void ShowProgramSettings()
    {
        SettingsCategories.SelectedIndex = 2;
        ((TabItem)SettingsCategories.Items[0]).Visibility = Visibility.Collapsed;
        ((TabItem)SettingsCategories.Items[1]).Visibility = Visibility.Collapsed;
        Title = "Regras de busca dos programas";
        SettingsHeading.Text = Title;
        SettingsDescription.Text = "Escolha quais vídeos procurar nos programas. Apenas vídeos encontrados poderão ser baixados.";
    }
    private void InitializeSettingsUi()
    {
        Loaded += (_,_) => { MaxHeight = SystemParameters.WorkArea.Height; MaxWidth = SystemParameters.WorkArea.Width; UiAccessibility.Apply(this); };
        SettingsCategories.SelectionChanged += (_,_) => Dispatcher.BeginInvoke(() => UiAccessibility.Apply(this));
        Closing += (_, e) =>
        {
            if (Saved) return;
            if (_saving) { e.Cancel = true; return; }
            if (_initialSettings is not null && _initialSettings != CaptureSettings())
            {
                var choice = UiDialog.ChooseUnsavedChanges(this);
                e.Cancel = choice != UiDialog.UnsavedChoice.Discard;
                if (choice == UiDialog.UnsavedChoice.Save)
                    Dispatcher.BeginInvoke(() => Save_Click(this, new RoutedEventArgs()));
            }
        };
    }
    private string CaptureSettings() => string.Join("|", ThemePreferenceCombo.SelectedIndex, ContentPathText.Text, CleanupEnabled.IsChecked, CleanupRetentionMonths.SelectedIndex, CleanupGracePeriodDays.Text, MissionsUrl.Text, ProvaiUrl.Text, HealthUrl.Text, MissionsPreviousSaturday.IsChecked, MissionsCurrentSaturday.IsChecked, MissionsNextSaturday.IsChecked, ProvaiPreviousSaturday.IsChecked, ProvaiCurrentSaturday.IsChecked, ProvaiNextSaturday.IsChecked, HealthPreviousSaturday.IsChecked, HealthCurrentSaturday.IsChecked, HealthNextSaturday.IsChecked);
    private bool ValidateSettings()
    {
        foreach(var input in new[] { MissionsUrl, ProvaiUrl, HealthUrl })
            if(!string.IsNullOrWhiteSpace(input.Text) && (!Uri.TryCreate(input.Text, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))) { SettingsFeedback.Text="Informe um endereço http ou https válido para o programa."; SettingsCategories.SelectedIndex=2; input.Focus(); return false; }
        if(!int.TryParse(CleanupGracePeriodDays.Text,out var days) || days<0 || days>180) { SettingsFeedback.Text="Informe uma tolerância entre 0 e 180 dias."; SettingsCategories.SelectedIndex=1; CleanupGracePeriodDays.Focus(); return false; }
        if(string.IsNullOrWhiteSpace(ContentPathText.Text)) { SettingsFeedback.Text="Escolha uma pasta de conteúdo."; SettingsCategories.SelectedIndex=1; return false; }
        return true;
    }
    private void SyncDownloadModes()
    {
        _syncingModes=true;
        MissionsWeeklyMode.IsChecked=!MissionsQuarterly.IsChecked.GetValueOrDefault(); MissionsQuarterMode.IsChecked=MissionsQuarterly.IsChecked;
        ProvaiWeeklyMode.IsChecked=!ProvaiQuarterly.IsChecked.GetValueOrDefault(); ProvaiQuarterMode.IsChecked=ProvaiQuarterly.IsChecked;
        HealthWeeklyMode.IsChecked=!HealthQuarterly.IsChecked.GetValueOrDefault(); HealthQuarterMode.IsChecked=HealthQuarterly.IsChecked;
        _syncingModes=false;
    }
    private void DownloadMode_Changed(object sender, RoutedEventArgs e)
    {
        if(_loading || _syncingModes || sender is not System.Windows.Controls.RadioButton { Tag: string prefix } mode) return;
        var previous=(System.Windows.Controls.CheckBox)FindName(prefix+"PreviousSaturday");
        var current=(System.Windows.Controls.CheckBox)FindName(prefix+"CurrentSaturday");
        var next=(System.Windows.Controls.CheckBox)FindName(prefix+"NextSaturday");
        _loading=true;
        if(mode.Name.EndsWith("QuarterMode")) { previous.IsChecked=false;current.IsChecked=false;next.IsChecked=false; }
        else if(!HasSaturdaySelection(previous,current,next)) current.IsChecked=true;
        _loading=false;RefreshQuarterlyAvailability();SyncDownloadModes();
    }
}
