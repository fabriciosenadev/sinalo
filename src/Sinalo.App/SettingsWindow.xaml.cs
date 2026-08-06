using System.Windows;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.App;

public partial class SettingsWindow : Window
{
    private readonly ISinaloConfigurationService _service;
    public bool Saved { get; private set; }
    public SettingsWindow(ISinaloConfigurationService service) { _service = service; InitializeComponent(); SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, SystemThemeService.IsWindowsDarkTheme()); Loaded += async (_, _) => { var items = await _service.LoadSourcesAsync(); MissionsUrl.Text = items.Single(x => x.Source == ContentSource.Missions).PageUrl; var provai = items.Single(x => x.Source == ContentSource.ProvaiEVede); ProvaiUrl.Text = provai.PageUrl; ProvaiPolicy.SelectedIndex = provai.Policy == AvailabilityPolicy.RollingSaturday ? 1 : 0; var health = items.Single(x => x.Source == ContentSource.Health); HealthUrl.Text = health.PageUrl; HealthPolicy.SelectedIndex = health.Policy == AvailabilityPolicy.RollingSaturday ? 1 : 0; }; }
    private async void Save_Click(object sender, RoutedEventArgs e) { var provaiPolicy = ProvaiPolicy.SelectedIndex == 1 ? AvailabilityPolicy.RollingSaturday : AvailabilityPolicy.QuarterlyFull; var healthPolicy = HealthPolicy.SelectedIndex == 1 ? AvailabilityPolicy.RollingSaturday : AvailabilityPolicy.QuarterlyFull; await _service.SaveSourcesAsync([new(ContentSource.Missions, "Informativo das Missões", MissionsUrl.Text, AvailabilityPolicy.MonthlyFull), new(ContentSource.ProvaiEVede, "Provai e Vede", ProvaiUrl.Text, provaiPolicy), new(ContentSource.Health, "Minuto de Saúde", HealthUrl.Text, healthPolicy)]); Saved = true; DialogResult = true; }
}
