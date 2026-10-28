using System.Windows;
using Sinalo.Application.Synchronization;

namespace Sinalo.App;
public partial class SettingsWindow
{
    private readonly IHealthYouTubeConfigurationService? _healthYouTubeConfiguration;
    private readonly IChannelVideoService? _channelVideos;
    private YouTubeChannelIdentity? _verifiedHealthChannel;
    private readonly CancellationTokenSource _channelCancellation = new();
    private bool _verifyingHealthChannel;
    private HealthYouTubeConfiguration ReadHealthChannelConfiguration() => new(
        HealthChannelEnabled.IsChecked == true, HealthChannelUrl.Text,
        _verifiedHealthChannel?.Id, _verifiedHealthChannel?.Name,
        HealthChannelHeight.SelectedIndex switch { 0 => 480, 2 => 1080, _ => 720 });
    private void HealthChannelUrl_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_loading) return;
        _verifiedHealthChannel = null;
        if (HealthChannelFeedback is not null) HealthChannelFeedback.Text = "Endereço alterado. Verifique o canal novamente antes de habilitar.";
    }
    private async void VerifyHealthChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_verifyingHealthChannel || _channelVideos is null) return;
        _verifyingHealthChannel = true;
        VerifyHealthChannelButton.IsEnabled = false;
        SaveSettingsButton.IsEnabled = false;
        HealthChannelUrl.IsEnabled = false;
        HealthChannelFeedback.Text = "Verificando identidade do canal, sem baixar vídeos...";
        try
        {
            var channel = await _channelVideos.VerifyAsync(HealthChannelUrl.Text, _channelCancellation.Token);
            _channelCancellation.Token.ThrowIfCancellationRequested();
            HealthChannelUrl.Text = channel.VideosUrl.AbsoluteUri;
            _verifiedHealthChannel = channel;
            HealthChannelFeedback.Text = $"Canal verificado: {channel.Name} · {channel.Id}";
        }
        catch (OperationCanceledException) when (_channelCancellation.IsCancellationRequested) { }
        catch (Exception exception) { _verifiedHealthChannel = null; HealthChannelFeedback.Text = $"Não foi possível verificar. {exception.Message}"; }
        finally { _verifyingHealthChannel = false; VerifyHealthChannelButton.IsEnabled = true; SaveSettingsButton.IsEnabled = true; HealthChannelUrl.IsEnabled = true; }
    }
}
