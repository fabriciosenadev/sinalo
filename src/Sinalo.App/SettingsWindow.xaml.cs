using System.Windows;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Sinalo.App;

public partial class SettingsWindow : Window
{
    private readonly ISinaloConfigurationService _service;
    private bool _loading;

    public bool Saved { get; private set; }
    public string ContentPath { get; } = new LocalSinaloPathService().GetPaths().ContentPath;

    public SettingsWindow(ISinaloConfigurationService service)
    {
        _service = service;
        InitializeComponent();
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

    private static DownloadSelection ReadSelection(CheckBox previous, CheckBox current, CheckBox next) => new(previous.IsChecked == true, current.IsChecked == true, next.IsChecked == true);

    private static AvailabilityPolicy PolicyFrom(DownloadSelection selection) => selection.DownloadsQuarterly ? AvailabilityPolicy.QuarterlyFull : AvailabilityPolicy.RollingSaturday;
}
