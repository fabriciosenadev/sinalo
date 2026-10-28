using Sinalo.App.ViewModels;
using Sinalo.Application.Catalog;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using static Sinalo.Tests.HealthYouTubeTestData;

namespace Sinalo.Tests.Unit;
public sealed class HealthYouTubeApprovalTests
{
    [Fact]
    public async Task ChangedConfigurationOrUnavailabilityNeverSubstitutesAnotherVideo()
    {
        var settings = new Settings { Current = Configuration with { Enabled = false } };
        var download = new Download(); var channel = new Channel();
        var executor = new LinkedVideoExecutionService(download, new Catalog(), new Categories(), channel, settings);
        var error = await Assert.ThrowsAsync<ChannelVideoApprovalRequiredException>(() => executor.ExecuteAsync(Request()));
        Assert.Equal(SynchronizationFailureCategory.ApprovalRequired, SynchronizationFailureClassifier.Classify(error, ContentSource.Health, "Saúde", null, SynchronizationStage.Discovery).Category);
        settings.Current = Configuration with { Revision = "new" };
        await Assert.ThrowsAsync<ChannelVideoApprovalRequiredException>(() => executor.ExecuteAsync(Request()));
        settings.Current = Configuration; channel.Current = null;
        await Assert.ThrowsAsync<ChannelVideoApprovalRequiredException>(() => executor.ExecuteAsync(Request()));
        Assert.Equal(0, download.Runs);
    }
    [Fact]
    public async Task ChangedTitleFormatOrSizeRequiresNewApproval()
    {
        var download = new Download(); var channel = new Channel();
        var executor = new LinkedVideoExecutionService(download, new Catalog(), new Categories(), channel, new Settings());
        var original = Video(1);
        channel.Current = original with { Video = original.Video with { Title = "Outro título" } };
        await Assert.ThrowsAsync<ChannelVideoApprovalRequiredException>(() => executor.ExecuteAsync(Request()));
        channel.Current = original with { Video = original.Video with { Formats = [original.Video.Formats[0] with { VideoFormatId = "other" }] } };
        await Assert.ThrowsAsync<ChannelVideoApprovalRequiredException>(() => executor.ExecuteAsync(Request()));
        channel.Current = original with { Video = original.Video with { Formats = [original.Video.Formats[0] with { EstimatedBytes = 100L * 1024 * 1024 }] } };
        await Assert.ThrowsAsync<ChannelVideoApprovalRequiredException>(() => executor.ExecuteAsync(Request()));
        Assert.Equal(0, download.Runs);
    }
    [Fact]
    public async Task FailureOrRefreshClearsOldSelection_AndCancelDoesNotApproveAnything()
    {
        var channel = new Channel(); var model = new HealthYouTubeViewModel(Configuration, channel, new Categories());
        await model.RefreshAsync(); model.Items[0].IsSelected = true;
        Assert.True(model.CanApprove);
        channel.Fail = true; await model.RefreshAsync();
        Assert.Empty(model.Items); Assert.False(model.CanApprove); Assert.Empty(model.CreateApprovedRequests());
        Assert.Contains("Não foi possível", model.Message);
        channel.Fail = false;
        using var cancellation = new CancellationTokenSource(); await cancellation.CancelAsync();
        await model.RefreshAsync(cancellation.Token); Assert.Empty(model.CreateApprovedRequests());
        Assert.Contains("cancelada", model.Message);
    }
    [Fact]
    public async Task ValidSelectionHasPublicationNotSaturdayAndCeilingIsFrozen()
    {
        var model = new HealthYouTubeViewModel(Configuration, new Channel(), new Categories());
        await model.RefreshAsync();
        Assert.Contains("720p", model.PolicyLabel);
        Assert.Contains("Publicado em", model.Items[0].PublicationLabel);
        model.Items[0].IsSelected = true;
        var request = Assert.Single(model.CreateApprovedRequests());
        Assert.True(request.LibraryStorage); Assert.Null(request.CustomUsageDate);
        Assert.Equal(Configuration, request.ChannelApproval!.Configuration);
        Assert.Equal(720, request.Format.Height);
    }
    private sealed class Catalog : IContentCatalog
    {
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken token = default) => Task.FromResult<IReadOnlyList<ContentItem>>([]);
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken token = default) => Task.CompletedTask;
    }
    private sealed class Download : ILinkedVideoService
    {
        public int Runs { get; private set; }
        public Task<LinkedVideo> InspectAsync(string url, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContentItem> DownloadAsync(LinkedVideoDownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        { Runs++; return Task.FromResult(new ContentItem(request.ItemId, ContentSource.Health, request.Video.Title, request.ScheduledDate, request.Video.PageUri, [])); }
    }
}
