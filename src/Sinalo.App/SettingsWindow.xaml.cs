using System.Windows;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Sinalo.App;

public partial class SettingsWindow : Window
{
    private readonly ISinaloConfigurationService _service;
    private readonly IContentPathConfigurationService? _contentPathConfigurationService;
    private readonly IContentPathMigrationService? _contentPathMigrationService;
    private bool _loading;

    public bool Saved { get; private set; }
    public SettingsWindow(ISinaloConfigurationService service, IContentPathConfigurationService? contentPathConfigurationService = null, IContentPathMigrationService? contentPathMigrationService = null)
    {
        _service = service;
        _contentPathConfigurationService = contentPathConfigurationService;
        _contentPathMigrationService = contentPathMigrationService;
        InitializeComponent();
        ContentPathText.Text = (_contentPathConfigurationService ?? new LocalSinaloPathService()).GetContentPath();
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, SystemThemeService.IsWindowsDarkTheme());
        Loaded += LoadConfigurationAsync;
    }

    private async void LoadConfigurationAsync(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            var items = await _service.LoadSourcesAsync();
            var missions = items.Single(x => x.Source == ContentSource.Missions);
            MissionsUrl.Text = missions.PageUrl;
            ApplySelection(missions.DownloadSelection ?? DownloadSelection.SaturdayWindow, MissionsPreviousSaturday, MissionsCurrentSaturday, MissionsNextSaturday, MissionsQuarterly);
            var provai = items.Single(x => x.Source == ContentSource.ProvaiEVede);
            ProvaiUrl.Text = provai.PageUrl;
            ApplySelection(provai.ResolvedDownloadSelection, ProvaiPreviousSaturday, ProvaiCurrentSaturday, ProvaiNextSaturday, ProvaiQuarterly);
            var health = items.Single(x => x.Source == ContentSource.Health);
            HealthUrl.Text = health.PageUrl;
            ApplySelection(health.ResolvedDownloadSelection, HealthPreviousSaturday, HealthCurrentSaturday, HealthNextSaturday, HealthQuarterly);
        }
        finally
        {
            _loading = false;
            RefreshQuarterlyAvailability();
        }
    }

    private void SaturdaySelection_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading) RefreshQuarterlyAvailability();
    }

    private void QuarterlySelection_Unchecked(object sender, RoutedEventArgs e)
    {
        var quarterly = (CheckBox)sender;
        var controls = GetSelectionControls(quarterly);
        if (!_loading && !HasSaturdaySelection(controls.Previous, controls.Current, controls.Next)) quarterly.IsChecked = true;
    }

    private void RefreshQuarterlyAvailability()
    {
        UpdateQuarterly(MissionsPreviousSaturday, MissionsCurrentSaturday, MissionsNextSaturday, MissionsQuarterly);
        UpdateQuarterly(ProvaiPreviousSaturday, ProvaiCurrentSaturday, ProvaiNextSaturday, ProvaiQuarterly);
        UpdateQuarterly(HealthPreviousSaturday, HealthCurrentSaturday, HealthNextSaturday, HealthQuarterly);
    }

    private (CheckBox Previous, CheckBox Current, CheckBox Next) GetSelectionControls(CheckBox quarterly) => quarterly == MissionsQuarterly
        ? (MissionsPreviousSaturday, MissionsCurrentSaturday, MissionsNextSaturday)
        : quarterly == ProvaiQuarterly
            ? (ProvaiPreviousSaturday, ProvaiCurrentSaturday, ProvaiNextSaturday)
            : (HealthPreviousSaturday, HealthCurrentSaturday, HealthNextSaturday);

    private static void UpdateQuarterly(CheckBox previous, CheckBox current, CheckBox next, CheckBox quarterly)
    {
        var hasSaturdaySelection = HasSaturdaySelection(previous, current, next);
        quarterly.IsChecked = !hasSaturdaySelection;
        quarterly.IsEnabled = !hasSaturdaySelection;
    }

    private static bool HasSaturdaySelection(CheckBox previous, CheckBox current, CheckBox next) => previous.IsChecked == true || current.IsChecked == true || next.IsChecked == true;

    private static void ApplySelection(DownloadSelection selection, CheckBox previous, CheckBox current, CheckBox next, CheckBox quarterly)
    {
        previous.IsChecked = selection.PreviousSaturday;
        current.IsChecked = selection.CurrentSaturday;
        next.IsChecked = selection.NextSaturday;
        quarterly.IsChecked = selection.DownloadsQuarterly;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_contentPathMigrationService is not null) await _contentPathMigrationService.MoveAsync(ContentPathText.Text);
            else _contentPathConfigurationService?.SaveContentPath(ContentPathText.Text);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, $"Não foi possível usar essa pasta para o conteúdo local.\n\n{exception.Message}", "Configurações", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var missionsSelection = ReadSelection(MissionsPreviousSaturday, MissionsCurrentSaturday, MissionsNextSaturday);
        var provaiSelection = ReadSelection(ProvaiPreviousSaturday, ProvaiCurrentSaturday, ProvaiNextSaturday);
        var healthSelection = ReadSelection(HealthPreviousSaturday, HealthCurrentSaturday, HealthNextSaturday);
        await _service.SaveSourcesAsync(
        [
            new(ContentSource.Missions, "Informativo das Missões", MissionsUrl.Text, PolicyFrom(missionsSelection), missionsSelection),
            new(ContentSource.ProvaiEVede, "Provai e Vede", ProvaiUrl.Text, PolicyFrom(provaiSelection), provaiSelection),
            new(ContentSource.Health, "Minuto de Saúde", HealthUrl.Text, PolicyFrom(healthSelection), healthSelection)
        ]);
        Saved = true;
        DialogResult = true;
    }

    private void ChooseContentPath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Escolha a pasta onde o Sinalo salvará os próximos vídeos.",
            UseDescriptionForTitle = true,
            SelectedPath = ContentPathText.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) ContentPathText.Text = dialog.SelectedPath;
    }

    private static DownloadSelection ReadSelection(CheckBox previous, CheckBox current, CheckBox next) => new(previous.IsChecked == true, current.IsChecked == true, next.IsChecked == true);

    private static AvailabilityPolicy PolicyFrom(DownloadSelection selection) => selection.DownloadsQuarterly ? AvailabilityPolicy.QuarterlyFull : AvailabilityPolicy.RollingSaturday;
}
