using System.IO;
using Microsoft.Data.Sqlite;
using Sinalo.Application.Configuration;
using Sinalo.Application.Appearance;
using Sinalo.Application.Storage;
using Sinalo.Application.Timer;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

[Collection(SqliteIntegrationCollection.Name)]
public sealed class StorageAndDatabaseTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LocalPathService_ShouldCreateEveryRequiredFolder()
    {
        var service = new LocalSinaloPathService(Path.Combine(_rootPath, "custom-content"));

        service.EnsureFolders();
        var paths = service.GetPaths();

        Assert.True(Directory.Exists(paths.DataPath));
        Assert.True(Directory.Exists(paths.ContentPath));
        Assert.True(Directory.Exists(paths.CachePath));
        Assert.True(Directory.Exists(paths.LogsPath));
        Assert.True(Directory.Exists(paths.TempDownloadsPath));
        Assert.Equal(Path.Combine(_rootPath, "custom-content"), paths.ContentPath);
    }

    [Fact]
    public void LocalPathService_ShouldPersistTheSelectedContentFolder()
    {
        var selectedContentPath = Path.Combine(_rootPath, "videos-em-outro-disco");
        var service = new LocalSinaloPathService(rootPath: _rootPath);

        service.SaveContentPath(selectedContentPath);

        var reloadedService = new LocalSinaloPathService(rootPath: _rootPath);
        Assert.Equal(selectedContentPath, reloadedService.GetContentPath());
        Assert.True(Directory.Exists(selectedContentPath));
    }

    [Fact]
    public async Task SinaloDatabase_ShouldCreateTheLocalDatabase()
    {
        var pathService = new TestPathService(_rootPath);
        var database = new SinaloDatabase(pathService);

        await database.InitializeAsync();

        Assert.True(File.Exists(pathService.GetPaths().DatabasePath));
        Assert.True(Directory.Exists(pathService.GetPaths().ContentPath));
    }

    [Fact]
    public async Task ConfigurationService_ShouldReturnTheThreeUnconfiguredDefaultSources()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);

        var sources = await service.LoadSourcesAsync();

        Assert.Collection(sources,
            source => { Assert.Equal(ContentSource.Missions, source.Source); Assert.Equal(AvailabilityPolicy.MonthlyFull, source.Policy); Assert.Empty(source.PageUrl); },
            source => { Assert.Equal(ContentSource.ProvaiEVede, source.Source); Assert.Equal(AvailabilityPolicy.QuarterlyFull, source.Policy); Assert.Empty(source.PageUrl); },
            source => { Assert.Equal(ContentSource.Health, source.Source); Assert.Equal(AvailabilityPolicy.QuarterlyFull, source.Policy); Assert.Equal("https://downloads.adventistas.org/pt/", source.PageUrl); });
    }

    [Fact]
    public async Task ConfigurationService_ShouldPersistTrimmedUrlsAndUpdateExistingSources()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);

        await service.SaveSourcesAsync(
        [
            new(ContentSource.Missions, "Informativo das Missões", " https://missions.example/ ", AvailabilityPolicy.MonthlyFull),
            new(ContentSource.ProvaiEVede, "Provai e Vede", "https://provai.example/", AvailabilityPolicy.RollingSaturday),
            new(ContentSource.Health, "Minuto de Saúde", "https://health.example/", AvailabilityPolicy.MonthlyFull)
        ]);
        await service.SaveSourcesAsync([new(ContentSource.Health, "Minuto de Saúde", "https://novo-health.example/", AvailabilityPolicy.MonthlyFull)]);

        var sources = await new SqliteConfigurationService(pathService).LoadSourcesAsync();

        Assert.Equal("https://missions.example/", sources.Single(source => source.Source == ContentSource.Missions).PageUrl);
        Assert.Equal("https://provai.example/", sources.Single(source => source.Source == ContentSource.ProvaiEVede).PageUrl);
        Assert.Equal(AvailabilityPolicy.RollingSaturday, sources.Single(source => source.Source == ContentSource.ProvaiEVede).Policy);
        Assert.Equal("https://novo-health.example/", sources.Single(source => source.Source == ContentSource.Health).PageUrl);
    }

    [Fact]
    public async Task ConfigurationService_ShouldMigrateTheLegacyHealthYoutubeUrlToTheOfficialDownloadsPortal()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);
        await service.SaveSourcesAsync([new(ContentSource.Health, "Minuto de Saúde", "https://www.youtube.com/@VidaeSaudeUCB", AvailabilityPolicy.MonthlyFull)]);

        var health = (await service.LoadSourcesAsync()).Single(source => source.Source == ContentSource.Health);

        Assert.Equal("https://downloads.adventistas.org/pt/", health.PageUrl);
        Assert.Equal(AvailabilityPolicy.QuarterlyFull, health.Policy);
    }

    [Fact]
    public async Task ConfigurationService_ShouldResolveLegacyPoliciesWithoutNewSelectionColumns()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);

        // O construtor anterior não informava DownloadSelection e, por isso, grava NULL
        // nas colunas novas, simulando uma instalação atualizada.
        await service.SaveSourcesAsync([new(ContentSource.ProvaiEVede, "Provai e Vede", "https://provai.example/", AvailabilityPolicy.RollingSaturday)]);

        var provai = (await service.LoadSourcesAsync()).Single(source => source.Source == ContentSource.ProvaiEVede);

        Assert.Null(provai.DownloadSelection);
        Assert.Equal(DownloadSelection.SaturdayWindow, provai.ResolvedDownloadSelection);
    }

    [Fact]
    public async Task ConfigurationService_ShouldPersistAnExplicitSaturdaySelection()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);
        var selection = new DownloadSelection(true, false, true);

        await service.SaveSourcesAsync([new(ContentSource.ProvaiEVede, "Provai e Vede", "https://provai.example/", AvailabilityPolicy.RollingSaturday, selection)]);

        var provai = (await service.LoadSourcesAsync()).Single(source => source.Source == ContentSource.ProvaiEVede);

        Assert.Equal(selection, provai.DownloadSelection);
        Assert.False(provai.ResolvedDownloadSelection.DownloadsQuarterly);
    }

    [Fact]
    public async Task PlaybackConfiguration_ShouldPersistTheSelectedScreen()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);

        Assert.Null((await service.LoadAsync()).FullscreenScreenNumber);
        await service.SaveAsync(new Sinalo.Application.Playback.PlaybackConfiguration(2, @"\\.\DISPLAY2"));

        var configuration = await service.LoadAsync();
        Assert.Equal(2, configuration.FullscreenScreenNumber);
        Assert.Equal(@"\\.\DISPLAY2", configuration.FullscreenMonitorKey);
    }

    [Fact]
    public async Task ThemePreference_ShouldDefaultToWindowsAndPersistTheSelectedTheme()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);

        Assert.Equal(ThemePreference.System, await ((IThemePreferenceService)service).LoadAsync());
        await ((IThemePreferenceService)service).SaveAsync(ThemePreference.Dark);

        Assert.Equal(ThemePreference.Dark, await ((IThemePreferenceService)service).LoadAsync());
    }

    [Fact]
    public async Task TimerConfiguration_ShouldPersistDirectionDurationAndFormat()
    {
        var pathService = new TestPathService(_rootPath);
        await new SinaloDatabase(pathService).InitializeAsync();
        var service = new SqliteConfigurationService(pathService);
        var expected = new TimerConfiguration(TimerDirection.CountDown, TimeSpan.FromMinutes(7), "nn:ss");

        await ((ITimerConfigurationService)service).SaveAsync(expected);

        Assert.Equal(expected, await ((ITimerConfigurationService)service).LoadAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class TestPathService(string rootPath) : ISinaloPathService
    {
        private readonly SinaloPaths _paths = new(
            rootPath,
            Path.Combine(rootPath, "data"),
            Path.Combine(rootPath, "content"),
            Path.Combine(rootPath, "cache"),
            Path.Combine(rootPath, "logs"),
            Path.Combine(rootPath, "temp", "downloads"),
            Path.Combine(rootPath, "data", "sinalo.db"));

        public SinaloPaths GetPaths() => _paths;

        public void EnsureFolders()
        {
            Directory.CreateDirectory(_paths.DataPath);
            Directory.CreateDirectory(_paths.ContentPath);
            Directory.CreateDirectory(_paths.CachePath);
            Directory.CreateDirectory(_paths.LogsPath);
            Directory.CreateDirectory(_paths.TempDownloadsPath);
        }
    }
}
