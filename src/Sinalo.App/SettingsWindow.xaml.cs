using System.Windows;
using Sinalo.Application.Appearance;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;

namespace Sinalo.App;

public partial class SettingsWindow : Window
{
    private readonly ISinaloConfigurationService _service;
    private readonly IContentPathConfigurationService? _contentPathConfigurationService;
    private readonly IContentPathMigrationService? _contentPathMigrationService;
    private readonly IThemePreferenceService? _themePreferenceService;
    private readonly IContentCleanupConfigurationService? _cleanupConfigurationService;
    private readonly SystemThemeService? _themeService;
    private ContentCleanupConfiguration _cleanupConfiguration = new();
    private bool _loading;

    public bool Saved { get; private set; }
    public SettingsWindow(ISinaloConfigurationService service, IContentPathConfigurationService? contentPathConfigurationService = null, IContentPathMigrationService? contentPathMigrationService = null, IThemePreferenceService? themePreferenceService = null, SystemThemeService? themeService = null, IContentCleanupConfigurationService? cleanupConfigurationService = null)
    {
        _service = service;
        _contentPathConfigurationService = contentPathConfigurationService;
        _contentPathMigrationService = contentPathMigrationService;
        _themePreferenceService = themePreferenceService;
        _themeService = themeService;
        _cleanupConfigurationService = cleanupConfigurationService;
        InitializeComponent();
        InitializeSettingsUi();
        ContentPathText.Text = (_contentPathConfigurationService ?? new LocalSinaloPathService()).GetContentPath();
        SourceInitialized += (_, _) => SystemThemeService.ApplyTitleBar(this, _themeService?.IsDark ?? SystemThemeService.IsWindowsDarkTheme());
        Loaded += LoadConfigurationAsync;
    }

    private async void LoadConfigurationAsync(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            if (_themePreferenceService is not null) ThemePreferenceCombo.SelectedIndex = (int)await _themePreferenceService.LoadAsync();
            if (_cleanupConfigurationService is not null)
            {
                var cleanup = _cleanupConfiguration = await _cleanupConfigurationService.LoadAsync();
                CleanupEnabled.IsChecked = cleanup.IsEnabled;
                CleanupGracePeriodDays.Text = cleanup.NormalizedGracePeriodDays.ToString();
                CleanupRetentionMonths.SelectedIndex = cleanup.NormalizedRetentionMonths switch { 1 => 0, 6 => 2, 12 => 3, _ => 1 };
            }
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
            SyncDownloadModes();
            _initialSettings = CaptureSettings();
        }
    }

    private void SaturdaySelection_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading) { RefreshQuarterlyAvailability(); SyncDownloadModes(); }
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
        if (_saving || _loading) return;
        SettingsFeedback.Text = "";
        if (!ValidateSettings()) return;
        var oldPath = (_contentPathConfigurationService ?? new LocalSinaloPathService()).GetContentPath();
        var moving = !string.Equals(System.IO.Path.TrimEndingDirectorySeparator(oldPath), System.IO.Path.TrimEndingDirectorySeparator(ContentPathText.Text), StringComparison.OrdinalIgnoreCase);
        if (moving && !UiDialog.Confirm(this, "Transferir vídeos?", $"Pasta atual: {oldPath}\nNova pasta: {ContentPathText.Text}\nOs vídeos existentes serão transferidos. Aguarde a conclusão antes de fechar.", "Transferir e salvar")) return;
        _saving = true; SettingsCategories.IsEnabled = false; SaveSettingsButton.IsEnabled = false; CancelSettingsButton.IsEnabled = false;
        SettingsFeedback.Text = moving ? "Transferindo vídeos e salvando configurações..." : "Salvando configurações...";
        var pathSaved = false;
        try
        {
            if (_contentPathMigrationService is not null) await _contentPathMigrationService.MoveAsync(ContentPathText.Text);
            else _contentPathConfigurationService?.SaveContentPath(ContentPathText.Text);
            pathSaved = moving;
            var missionsSelection = ReadSelection(MissionsPreviousSaturday, MissionsCurrentSaturday, MissionsNextSaturday);
            var provaiSelection = ReadSelection(ProvaiPreviousSaturday, ProvaiCurrentSaturday, ProvaiNextSaturday);
            var healthSelection = ReadSelection(HealthPreviousSaturday, HealthCurrentSaturday, HealthNextSaturday);
            await _service.SaveSourcesAsync([
                new(ContentSource.Missions, "Informativo das Missões", MissionsUrl.Text, PolicyFrom(missionsSelection), missionsSelection),
                new(ContentSource.ProvaiEVede, "Provai e Vede", ProvaiUrl.Text, PolicyFrom(provaiSelection), provaiSelection),
                new(ContentSource.Health, "Minuto de Saúde", HealthUrl.Text, PolicyFrom(healthSelection), healthSelection)]);
            if (_cleanupConfigurationService is not null)
            {
                var months = CleanupRetentionMonths.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var selectedMonths) ? selectedMonths : 3;
                await _cleanupConfigurationService.SaveAsync(new ContentCleanupConfiguration(CleanupEnabled.IsChecked == true, months, int.Parse(CleanupGracePeriodDays.Text), _cleanupConfiguration.LastRunDate));
            }
            if (_themePreferenceService is not null)
            {
                var preference = (ThemePreference)Math.Clamp(ThemePreferenceCombo.SelectedIndex, 0, 2);
                await _themePreferenceService.SaveAsync(preference); _themeService?.SetPreference(preference);
            }
            Saved = true; _saving = false; DialogResult = true;
        }
        catch (Exception exception)
        {
            var recovery = exception is UnauthorizedAccessException ? "Verifique a permissão de acesso à pasta." : exception is System.IO.IOException ? "Verifique o espaço livre e o acesso à pasta." : "Confira os campos e tente salvar novamente. Se persistir, procure suporte.";
            SettingsFeedback.Text = (pathSaved ? "A pasta foi transferida, mas outras configurações não foram concluídas. " : "Não foi possível concluir o salvamento. ") + recovery;
        }
        finally { _saving = false; SettingsCategories.IsEnabled = true; SaveSettingsButton.IsEnabled = true; CancelSettingsButton.IsEnabled = true; }
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

    private void RestoreOfficialUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sourceName } ||
            !Enum.TryParse<ContentSource>(sourceName, out var source)) return;

        var officialUrl = OfficialContentPrograms.Get(source).PageUrl;
        switch (source)
        {
            case ContentSource.Missions:
                MissionsUrl.Text = officialUrl;
                break;
            case ContentSource.ProvaiEVede:
                ProvaiUrl.Text = officialUrl;
                break;
            case ContentSource.Health:
                HealthUrl.Text = officialUrl;
                break;
        }
    }

    private static DownloadSelection ReadSelection(CheckBox previous, CheckBox current, CheckBox next) => new(previous.IsChecked == true, current.IsChecked == true, next.IsChecked == true);

    private static AvailabilityPolicy PolicyFrom(DownloadSelection selection) => selection.DownloadsQuarterly ? AvailabilityPolicy.QuarterlyFull : AvailabilityPolicy.RollingSaturday;
}
