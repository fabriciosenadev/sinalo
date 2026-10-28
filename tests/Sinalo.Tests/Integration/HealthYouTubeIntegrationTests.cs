using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;
using static Sinalo.Tests.HealthYouTubeTestData;

namespace Sinalo.Tests.Integration;
[Collection(SqliteIntegrationCollection.Name)]
public sealed class HealthYouTubeIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));
    private LocalSinaloPathService Paths => new(rootPath: _root);
    private readonly PlaybackActivityGate _activity = new();
    private readonly ContentOperationGate _gate = new();
    [Fact]
    public async Task ConfigurationMigration_IsAdditiveDisabledAndHasStableRevisionUntilChanges()
    {
        await new SinaloDatabase(Paths).InitializeAsync();
        var sources = new SqliteConfigurationService(Paths);
        var legacy = Sinalo.Application.Configuration.OfficialContentPrograms.Health with { PageUrl = "https://example.test/health/" };
        await sources.SaveSourcesAsync([legacy]);
        await new SinaloDatabase(Paths).InitializeAsync();
        var store = new HealthYouTubeConfigurationService(Paths);
        Assert.False((await store.LoadAsync()).Enabled);
        Assert.Null((await store.LoadAsync()).ChannelId);
        await store.SaveAsync(Configuration);
        var saved = await store.LoadAsync();
        Assert.True(saved.Enabled); Assert.NotEmpty(saved.Revision); Assert.Equal(ChannelId, saved.ChannelId);
        await store.SaveAsync(saved); Assert.Equal(saved, await store.LoadAsync());
        await store.SaveAsync(saved with { MaximumHeight = 480 }); Assert.NotEqual(saved.Revision, (await store.LoadAsync()).Revision);
        Assert.Equal(legacy.PageUrl, (await sources.LoadSourcesAsync()).Single(x => x.Source == ContentSource.Health).PageUrl);
    }
    [Fact]
    public async Task ApprovedDownload_PreservesGlobalIdentityMovedCategoryHistoryAndOptionalUsageDate()
    {
        await new SinaloDatabase(Paths).InitializeAsync();
        var library = new SqliteLibraryRepository(Paths);
        var categories = new SqliteMediaCategoryService(Paths, library, _gate, _activity, () => false);
        var catalog = new SqliteContentCatalog(Paths);
        var settings = new Settings();
        var runs = 0;
        var downloader = Downloader(() => runs++);
        var executor = new LinkedVideoExecutionService(downloader, catalog, categories, new Channel(), settings);
        var request = Request();

        var downloaded = (await executor.ExecuteAsync(request))!;
        var media = (await library.FindAsync(downloaded.Id))!;
        Assert.True(media.CanPlay); Assert.Null(media.UsageDate);
        Assert.Equal(new DateOnly(2026, 10, 21), media.PublishedDate);
        Assert.Equal(ChannelId, media.ProviderChannelId); Assert.True(media.IsPinned);
        Assert.Contains("YouTube", media.OriginLabel); Assert.Contains("21/10/2026", media.PublicationLabel);
        Assert.Single((await library.QueryAsync(new(Search: "21/10/2026"))).Items);
        Assert.Null(await catalog.FindByIdAsync(downloaded.Id));
        Assert.Equal(1, runs); Assert.Null(await executor.ExecuteAsync(request)); Assert.Equal(1, runs);

        var category = await categories.CreateAsync("Vídeos preservados");
        await categories.AssignAsync([media.Id], category.Id);
        await library.RenameAsync(media.Id, "Nome escolhido", new DateOnly(2026, 10, 31));
        await library.RecordPlaybackAsync(media.Id);
        Assert.Null(await executor.ExecuteAsync(request)); Assert.Equal(1, runs);
        var moved = (await categories.FindLinkedAnywhereAsync(request.Video.Id))!;
        Assert.Equal(category.Id, moved.CategoryId);
        File.Delete(moved.LocalPath!);
        var restored = (await executor.ExecuteAsync(request))!;
        var final = (await library.FindAsync(media.Id))!;
        Assert.Equal(2, runs); Assert.Equal(media.Id, restored.Id);
        Assert.Equal(moved.LocalPath, final.LocalPath);
        Assert.Equal(category.Id, final.CategoryId); Assert.Equal("Nome escolhido", final.Name);
        Assert.Equal(1, final.PlayCount); Assert.True(final.IsPinned);
        Assert.Equal(new DateOnly(2026, 10, 31), final.UsageDate);
        Assert.Equal(new DateOnly(2026, 10, 21), final.PublishedDate);
        Assert.Single((await library.QueryAsync(new())).Items);
    }
    [Fact]
    public async Task SelectionIsNotDownload_AndOnlyExplicitlyApprovedIdsExecuteInTheExistingQueue()
    {
        await new SinaloDatabase(Paths).InitializeAsync();
        var categories = new SqliteMediaCategoryService(Paths, new SqliteLibraryRepository(Paths), _gate, _activity, () => false);
        var channel = new Channel { Inspector = id => Video(int.Parse(id[5..])) };
        var model = new HealthYouTubeViewModel(Configuration, channel, categories);
        var runs = 0;
        var execute = new LinkedVideoExecutionService(Downloader(() => runs++), new SqliteContentCatalog(Paths), categories, channel, new Settings());
        var queue = new SynchronizationQueue(async (job, _, token) => new(await execute.ExecuteAsync(job.LinkedVideo!, token: token) is null ? 0 : 1));
        await model.RefreshAsync();
        Assert.Equal(4, model.Items.Count); Assert.All(model.Items, x => Assert.False(x.IsSelected));
        Assert.False(model.CanApprove); Assert.Empty(model.CreateApprovedRequests()); Assert.Equal(0, runs);
        model.Items[1].IsSelected = true; Assert.Equal(0, runs);
        var approved = Assert.Single(model.CreateApprovedRequests());
        Assert.Equal(Id(2), approved.Video.Id);
        Assert.True(queue.Enqueue(new(Sinalo.Application.Configuration.OfficialContentPrograms.Health, LinkedVideo: approved)).Added);
        await queue.WhenIdleAsync();
        Assert.Equal(1, runs); Assert.Equal(SynchronizationQueueState.Completed, Assert.Single(queue.GetSnapshot().Entries).State);
        var item = Assert.Single((await new SqliteLibraryRepository(Paths).QueryAsync(new())).Items);
        Assert.Equal("health-youtube-" + Id(2), item.Id);
        await model.RefreshAsync();
        Assert.Equal(4, model.Items.Count); Assert.False(model.Items.Single(x => x.Candidate.Video.Id == Id(2)).CanSelect);
        Assert.All(model.Items, x => Assert.False(x.IsSelected));
    }
    [Fact]
    public async Task DiskFailureStopsOnlyTheApprovedBatchAndNeverStartsItsRemainingDownloads()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = new List<string>();
        var queue = new SynchronizationQueue(async (job, _, _) =>
        {
            runs.Add(job.LinkedVideo!.Video.Id);
            if (job.LinkedVideo.Video.Id == Id(1))
            {
                started.TrySetResult(); await release.Task;
                throw new StorageSpaceCriticalException();
            }
            return new(1);
        });
        SynchronizationQueueRequest Job(int id, string batch)
        {
            var request = Request(Video(id));
            return new(Sinalo.Application.Configuration.OfficialContentPrograms.Health,
                LinkedVideo: request with { ChannelApproval = request.ChannelApproval! with { BatchId = batch } });
        }
        queue.Enqueue(Job(1, "batch")); await started.Task;
        Assert.False(queue.Enqueue(Job(1, "batch")).Added);
        queue.Enqueue(Job(2, "batch")); queue.Enqueue(Job(3, "other")); release.TrySetResult();
        await queue.WhenIdleAsync();
        Assert.Equal([Id(1), Id(3)], runs);
        Assert.Equal([SynchronizationQueueState.Failed, SynchronizationQueueState.Cancelled, SynchronizationQueueState.Completed], queue.GetSnapshot().Entries.Select(x => x.State));
    }
    private LinkedVideoService Downloader(Action downloaded) => new(Paths, toolDirectory: _root, toolRunner: async (args, output, token) =>
    {
        var template = args[args.ToList().IndexOf("--output") + 1];
        var id = args[^1].Split('=')[1];
        var bytes = new byte[4096]; Encoding.ASCII.GetBytes("ftyp").CopyTo(bytes, 4);
        await File.WriteAllBytesAsync(Path.Combine(Path.GetDirectoryName(template)!, id + ".mp4"), bytes, token);
        output?.Invoke("100%"); downloaded(); return new VideoToolResult(0, "", "");
    }, freeSpace: _ => 10L * 1024 * 1024 * 1024);
    [Fact]
    public async Task ApprovedDownloadRejectsInsufficientSpaceAndExternalReplacementBeforeStartingTool()
    {
        var service = new LinkedVideoService(Paths, toolDirectory: _root,
            toolRunner: (_, _, _) => throw new Exception("Must not start"), freeSpace: _ => 0);
        await Assert.ThrowsAsync<InsufficientStorageSpaceException>(() => service.DownloadAsync(Request()));
        var outside = Path.Combine(Path.GetDirectoryName(_root)!, "not-owned.mp4");
        await Assert.ThrowsAsync<IOException>(() => service.DownloadAsync(Request() with { ExistingManagedPath = outside }));
        Assert.False(File.Exists(outside));
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
