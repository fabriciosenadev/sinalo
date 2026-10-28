using System.Windows;
using Sinalo.App.ViewModels;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.App;
public partial class MainWindow
{
    internal async void ConsultHealthYouTube_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel model || model.SelectedSource != "Minuto de Saúde") return;
        if (HealthYouTubeConfiguration is null || ChannelVideos is null || Categories is null || ConfigurationService is null || SynchronizationQueue is null)
        { model.OperationMessage = "A fonte secundária do YouTube não está disponível nesta execução."; return; }
        try
        {
            var configuration = await HealthYouTubeConfiguration.LoadAsync();
            if (!configuration.Enabled)
            { model.OperationMessage = "Habilite e verifique o canal em Configurações > Programas de vídeo > Minuto de Saúde > Fonte secundária — YouTube."; return; }
            var dialog = new HealthYouTubeWindow(new HealthYouTubeViewModel(configuration, ChannelVideos, Categories), ThemeService) { Owner = this };
            if (dialog.ShowDialog() != true) { model.OperationMessage = "Consulta encerrada sem novos downloads."; return; }
            var source = (await ConfigurationService.LoadSourcesAsync()).Single(x => x.Source == ContentSource.Health);
            var added = 0;
            foreach (var approved in dialog.ApprovedRequests)
                if (SynchronizationQueue.Enqueue(new SynchronizationQueueRequest(source, LinkedVideo: approved)).Added) added++;
            model.OperationMessage = $"{added} vídeo(s) aprovado(s) adicionado(s) à fila. A disponibilidade e os arquivos locais serão conferidos novamente.";
        }
        catch (Exception exception) { model.OperationMessage = $"Não foi possível consultar a fonte secundária. {exception.Message}"; }
    }
}
