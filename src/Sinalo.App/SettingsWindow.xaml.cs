using System.Windows;
using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.App;

public partial class SettingsWindow : Window
{
    private readonly ISinaloConfigurationService _service;
    public bool Saved { get; private set; }
    public SettingsWindow(ISinaloConfigurationService service) { _service = service; InitializeComponent(); Loaded += async (_, _) => { var items = await _service.LoadSourcesAsync(); MissionsUrl.Text = items.Single(x => x.Source == ContentSource.Missions).PageUrl; ProvaiUrl.Text = items.Single(x => x.Source == ContentSource.ProvaiEVede).PageUrl; HealthUrl.Text = items.Single(x => x.Source == ContentSource.Health).PageUrl; }; }
    private async void Save_Click(object sender, RoutedEventArgs e) { await _service.SaveSourcesAsync([new(ContentSource.Missions, "Informativo das Missões", MissionsUrl.Text, AvailabilityPolicy.MonthlyFull), new(ContentSource.ProvaiEVede, "Provai e Vede", ProvaiUrl.Text, AvailabilityPolicy.QuarterlyFull), new(ContentSource.Health, "Minuto de Saúde", HealthUrl.Text, AvailabilityPolicy.MonthlyFull)]); Saved = true; DialogResult = true; }
}
