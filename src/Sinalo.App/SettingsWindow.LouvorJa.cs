using System.Windows;
using Sinalo.Application.LouvorJa;

namespace Sinalo.App;

public partial class SettingsWindow
{
    private readonly ILouvorJaConfigurationStore? _louvorJaConfiguration;
    private bool _louvorJaLoaded;
    private bool _initialLouvorJaEnabled;

    private async Task LoadLouvorJaPreferenceAsync()
    {
        _louvorJaLoaded = false;
        LouvorJaFluteEnabled.IsEnabled = false;
        if (_louvorJaConfiguration is null)
        { LouvorJaSettingsFeedback.Text = "Configuração da integração não disponível nesta janela."; return; }
        try
        {
            var saved = await _louvorJaConfiguration.LoadAsync();
            LouvorJaFluteEnabled.IsChecked = _initialLouvorJaEnabled = saved?.Enabled ?? false;
            _louvorJaLoaded = true; LouvorJaFluteEnabled.IsEnabled = true;
            ReloadLouvorJaSettingsButton.Visibility = Visibility.Collapsed;
            LouvorJaSettingsFeedback.Text = "A escolha será aplicada somente ao salvar as configurações.";
        }
        catch (Exception)
        {
            LouvorJaFluteEnabled.IsChecked = false;
            LouvorJaSettingsFeedback.Text = "Não foi possível ler a configuração do Louvor JA Flute. Confira acesso e integridade de louvor-ja-integration.json na pasta de dados do Sinalo e tente novamente. O arquivo não foi substituído.";
            ReloadLouvorJaSettingsButton.Visibility = Visibility.Visible;
        }
    }
    private async void ReloadLouvorJaSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_loading || _saving) return;
        var unchanged = _initialSettings == CaptureSettings();
        _loading = true; ReloadLouvorJaSettingsButton.IsEnabled = false;
        try { await LoadLouvorJaPreferenceAsync(); if (unchanged) _initialSettings = CaptureSettings(); }
        finally { _loading = false; ReloadLouvorJaSettingsButton.IsEnabled = true; }
    }
}
