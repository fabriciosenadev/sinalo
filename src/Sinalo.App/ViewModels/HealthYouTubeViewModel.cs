using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Library;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public sealed class ChannelVideoChoice(ChannelVideo candidate, LinkedVideoFormat format, bool downloaded) : ObservableObject
{
    public ChannelVideo Candidate { get; } = candidate;
    public LinkedVideoFormat Format { get; } = format;
    public bool CanSelect => !downloaded;
    public string Title => Candidate.Video.Title;
    public string EpisodeLabel => Candidate.Episode is { } episode ? $"Episódio {episode}" : "Vídeo do canal";
    public string PublicationLabel => Candidate.PublishedAt is { } at ? $"Publicado em {at.LocalDateTime:dd/MM/yyyy HH:mm}" : $"Publicado em {Candidate.Video.PublishedDate:dd/MM/yyyy} (data UTC)";
    public string Status => downloaded ? "Já baixado — não será baixado novamente" : $"Disponível para baixar · {Format.Label}";
    private bool _selected;
    public bool IsSelected { get => _selected; set => SetProperty(ref _selected, value && CanSelect); }
}

public sealed class HealthYouTubeViewModel(HealthYouTubeConfiguration configuration, IChannelVideoService service, IMediaCategoryService categories) : ObservableObject
{
    public ObservableCollection<ChannelVideoChoice> Items { get; } = [];
    public string ChannelName => configuration.ChannelName ?? "YouTube";
    public string PolicyLabel => $"Até 4 vídeos recentes publicados · máximo {configuration.MaximumHeight}p · YouTube";
    private bool _busy;
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); NotifySelection(); OnPropertyChanged(nameof(CanRefresh)); OnPropertyChanged(nameof(RefreshCaption)); OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(ShowEmptyState)); } }
    private string _message = "Consulte os vídeos. Nenhum download será iniciado sem sua confirmação.";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool CanRefresh => !IsBusy;
    public string RefreshCaption => IsBusy ? "Buscando…" : "Atualizar lista";
    public bool HasResults => !IsBusy && Items.Count > 0;
    public bool ShowEmptyState => !IsBusy && Items.Count == 0;
    public bool CanApprove => !IsBusy && Items.Any(x => x.IsSelected && x.CanSelect);
    public string ApprovalCaption => $"Baixar selecionados ({Items.Count(x => x.IsSelected && x.CanSelect)})";
    public string Summary
    {
        get
        {
            if (IsBusy) return "Busca em andamento — aguarde para selecionar os vídeos.";
            var selected = Items.Where(x => x.IsSelected && x.CanSelect).ToArray();
            var known = selected.Sum(x => x.Format.EstimatedBytes ?? 0);
            return $"{selected.Length} selecionado(s) · {known / 1024d / 1024:0} MB estimados" + (selected.Any(x => x.Format.EstimatedBytes is null) ? " + tamanhos não informados" : "");
        }
    }
    private void NotifySelection() { OnPropertyChanged(nameof(CanApprove)); OnPropertyChanged(nameof(ApprovalCaption)); OnPropertyChanged(nameof(Summary)); }
    public async Task RefreshAsync(CancellationToken token = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        Items.Clear();
        Message = "Consultando os vídeos publicados e conferindo disponibilidade...";
        try
        {
            var result = await service.ListRecentAsync(configuration, token);
            foreach (var candidate in result.Videos)
            {
                token.ThrowIfCancellationRequested();
                var format = ChannelVideoSelection.Format(candidate.Video, configuration.MaximumHeight);
                if (format is null) continue;
                var existing = await categories.FindLinkedAnywhereAsync(candidate.Video.Id, token);
                var choice = new ChannelVideoChoice(candidate, format, existing?.CanPlay == true && File.Exists(existing.LocalPath));
                choice.PropertyChanged += (_, _) => NotifySelection();
                Items.Add(choice);
            }
            Message = (result.IsLimited ? "Resultado parcial/limitado. " : "") +
                (Items.Count == 0 ? "Nenhum vídeo publicado e compatível foi encontrado." : $"{Items.Count} vídeo(s). Marque os desejados e confirme. Agendados não entram na lista.") +
                (result.Excluded > 0 ? $" {result.Excluded} entrada(s) excluída(s) da consulta." : "") +
                (result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : "");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { Items.Clear(); Message = "Consulta cancelada. Nenhum download iniciado."; }
        catch (Exception exception) { Items.Clear(); Message = $"Não foi possível consultar o canal. {exception.Message}"; }
        finally { IsBusy = false; }
    }
    public IReadOnlyList<LinkedVideoDownloadRequest> CreateApprovedRequests()
    {
        if (!CanApprove) return [];
        var batch = Guid.NewGuid().ToString("N");
        return Items.Where(x => x.CanSelect && x.IsSelected).Select(x => new LinkedVideoDownloadRequest(
            x.Candidate.Video, x.Format, ContentSource.Health, x.Candidate.Video.PublishedDate,
            CategoryId: MediaCategory.ForSource(ContentSource.Health), LibraryStorage: true,
            ChannelApproval: new(x.Candidate, configuration, batch))).ToArray();
    }
}
