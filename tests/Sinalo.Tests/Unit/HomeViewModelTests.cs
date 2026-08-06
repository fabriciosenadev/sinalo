using Sinalo.App.ViewModels;
using Sinalo.Application.Configuration;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Unit;

public sealed class HomeViewModelTests
{
    [Fact]
    public void Constructor_ShouldSelectPersistedPlaybackScreen()
    {
        var screens = new[] { new PlaybackScreenOption("Abrir normalmente", null), new PlaybackScreenOption("Tela 2", 2) };
        var viewModel = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), [], playbackScreens: screens, selectedPlaybackScreenNumber: 2);

        Assert.Equal(2, viewModel.SelectedPlaybackScreen!.ScreenNumber);
    }

    [Fact]
    public void Filters_ShouldCombineSourceAvailabilityAndSearch()
    {
        var viewModel = CreateViewModel(
        [
            Item("provai-ready", ContentSource.ProvaiEVede, "Pão de cada dia", SyncState.Ready),
            Item("mission-pending", ContentSource.Missions, "Informativo mundial", SyncState.Pending),
            Item("health-online", ContentSource.Health, "Vida e saúde", SyncState.OnlineOnly)
        ]);

        viewModel.SelectedSource = "Provai e Vede";
        viewModel.SelectedAvailability = "Pronto offline";
        viewModel.SearchQuery = "pão";

        var item = Assert.Single(viewModel.CatalogItems);
        Assert.Equal("provai-ready", item.Id);
        Assert.Equal("Pronto offline", item.Status);
    }

    [Fact]
    public void SourceActions_ShouldFollowTheSelectedSourceAndDisableHealthUntilSupported()
    {
        var viewModel = CreateViewModel([]);

        Assert.False(viewModel.CanOperateSelectedSource);
        Assert.Equal("Selecione uma fonte", viewModel.SelectedSourceActionLabel);

        viewModel.SelectedSource = "Informativo das Missões";
        Assert.True(viewModel.CanOperateSelectedSource);
        Assert.Equal("Informativo das Missões", viewModel.SelectedSourceActionLabel);

        viewModel.SelectedSource = "Minuto de Saúde";
        Assert.False(viewModel.CanOperateSelectedSource);
        Assert.True(viewModel.IsHealthSelected);
    }

    [Fact]
    public void Schedule_ShouldAddOnlyOnceRemoveAndReorderItems()
    {
        var viewModel = CreateViewModel(
        [
            Item("first", ContentSource.Missions, "Primeiro", SyncState.Ready),
            Item("second", ContentSource.ProvaiEVede, "Segundo", SyncState.Ready)
        ]);

        viewModel.SelectedCatalogItem = viewModel.CatalogItems[0];
        viewModel.AddSelectedToSchedule();
        viewModel.AddSelectedToSchedule();
        viewModel.SelectedCatalogItem = viewModel.CatalogItems[1];
        viewModel.AddSelectedToSchedule();
        viewModel.MoveScheduleItem(viewModel.ScheduleItems[1], -1);

        Assert.Equal(["second", "first"], viewModel.ScheduleItems.Select(item => item.Id));
        viewModel.RemoveFromSchedule(viewModel.ScheduleItems[0]);
        Assert.Single(viewModel.ScheduleItems);
        Assert.Contains("removido", viewModel.OperationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModel_ShouldExposeAllCatalogStatesAndSafelyIgnoreInvalidScheduleActions()
    {
        var viewModel = CreateViewModel(
        [
            new ContentItem("page", ContentSource.ProvaiEVede, "Página", new DateOnly(2026, 8, 8), new Uri("https://example.test/page"), []),
            Item("failed", ContentSource.Missions, "Falha", SyncState.Failed),
            Item("online", ContentSource.Health, "Online", SyncState.OnlineOnly),
            Item("ready", ContentSource.ProvaiEVede, "Pronto", SyncState.Ready)
        ]);

        Assert.Equal("Selecione um vídeo", viewModel.SelectedItemTitle);
        Assert.False(viewModel.HasSelectedItem);
        Assert.Contains(viewModel.CatalogItems, item => item.Status == "Página trimestral identificada" && item.ThumbnailGlyph == "↓");
        Assert.Contains(viewModel.CatalogItems, item => item.Status == "Falhou");
        Assert.Contains(viewModel.CatalogItems, item => item.Status == "Somente online" && item.ThumbnailGlyph == "◌");
        Assert.Contains(viewModel.CatalogItems, item => item.Status == "Pronto offline" && item.ThumbnailGlyph == "▶");

        viewModel.AddSelectedToSchedule();
        viewModel.RemoveFromSchedule(null);
        viewModel.MoveScheduleItem(null, 1);
        viewModel.SelectedCatalogItem = viewModel.CatalogItems.Single(item => item.Id == "ready");
        Assert.True(viewModel.HasSelectedItem);
        Assert.Contains("Pronto offline", viewModel.SelectedItemDetails);
        Assert.Equal("Arquivo local ainda não disponível.", viewModel.SelectedItemPath);
        viewModel.AddSelectedToSchedule();
        viewModel.MoveScheduleItem(viewModel.ScheduleItems[0], 1);
        Assert.Single(viewModel.ScheduleItems);
    }

    [Fact]
    public void DownloadProgress_ShouldUpdateTheFeedbackAndCatalogImmediately()
    {
        var pending = Item("pending", ContentSource.ProvaiEVede, "Conteúdo semanal", SyncState.Pending);
        var viewModel = CreateViewModel([pending]);
        var ready = pending with { SyncState = SyncState.Ready, LocalPath = "C:\\Sinalo\\conteudo.mp4" };

        viewModel.ReportDownloadProgress(new Sinalo.Application.Synchronization.DownloadProgress(ready, 50, 100, "Baixando"));
        Assert.Equal(50, viewModel.SyncProgressPercent);
        Assert.Contains("50", viewModel.SyncProgressLabel);

        viewModel.ReportDownloadProgress(new Sinalo.Application.Synchronization.DownloadProgress(ready, 100, 100, "Disponível offline"));
        Assert.Equal("Pronto offline", viewModel.CatalogItems.Single().Status);
        Assert.Equal("Conteúdo semanal", viewModel.SelectedItemTitle);
        Assert.Equal("C:\\Sinalo\\conteudo.mp4", viewModel.SelectedItemPath);
    }

    private static HomeViewModel CreateViewModel(IReadOnlyList<ContentItem> items) => new(
        new SaturdayWindowService(),
        new LocalSinaloPathService(),
        [
            new(ContentSource.Missions, "Informativo das Missões", "https://missions.example/", AvailabilityPolicy.MonthlyFull),
            new(ContentSource.ProvaiEVede, "Provai e Vede", "https://provai.example/", AvailabilityPolicy.QuarterlyFull),
            new(ContentSource.Health, "Minuto de Saúde", "https://health.example/", AvailabilityPolicy.MonthlyFull)
        ],
        items);

    private static ContentItem Item(string id, ContentSource source, string title, SyncState state) => new(
        id, source, title, new DateOnly(2026, 8, 8), new Uri("https://example.test/" + id),
        [new MediaAsset(id + "-asset", new Uri("https://example.test/" + id + ".mp4"), id + ".mp4", null, null)], state);
}
