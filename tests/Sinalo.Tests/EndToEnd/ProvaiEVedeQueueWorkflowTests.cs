using System.Net;
using System.Net.Http;
using Sinalo.App;
using Sinalo.Application.Catalog;
using Sinalo.Application.Configuration;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class ProvaiEVedeQueueWorkflowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MainWindowQueue_ShouldNotDownloadTheOtherVersion(bool libras, bool manual)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var expected = libras ? "provai-e-vede-libras-2026-10-24" : "provai-e-vede-2026-10-24";
                var other = libras ? "provai-e-vede-2026-10-24" : "provai-e-vede-libras-2026-10-24";
                var catalog = new Catalog(other);
                var downloader = new Downloader();
                using var client = new HttpClient(new Html());
                var discovery = new ContentDiscoveryService([new ProvaiEVedeDiscoveryConnector(client)], catalog);
                var window = new MainWindow
                {
                    ContentCatalog = catalog,
                    DiscoveryService = discovery,
                    ProvaiEVedeSynchronizationService = new ProvaiEVedeSynchronizationService(catalog, downloader),
                    ManualSynchronizationService = new ManualContentSynchronizationService(catalog, downloader)
                };
                var configuration = new SourceConfiguration(ContentSource.ProvaiEVede, "Provai e Vede",
                    $"https://downloads.test/provai-e-vede-2026-4o-trimestre{(libras ? "-libras" : "")}/", AvailabilityPolicy.QuarterlyFull);
                var queue = window.CreateSynchronizationQueue();
                queue.Enqueue(new SynchronizationQueueRequest(configuration, manual ? [expected, other] : null));
                queue.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
                var entry = Assert.Single(queue.GetSnapshot().Entries);
                Assert.Equal(SynchronizationQueueState.Completed, entry.State);
                Assert.Equal(1, entry.ReadyItems);
                Assert.Equal(expected, Assert.Single(downloader.Ids));
                Assert.NotNull(catalog.Items.SingleOrDefault(item => item.Id == other));
                window.Close();
            }
            catch (Exception caught) { error = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "O fluxo da fila não terminou.");
        Assert.Null(error);
    }

    private sealed class Html : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"<tr><td>(24/10) Vídeo</td><td>140MB</td><td><a href='https://files.test/{(request.RequestUri!.AbsolutePath.Contains("-libras") ? "libras" : "standard")}.mp4'>Baixar</a></td></tr>")
            });
    }

    private sealed class Catalog(string other) : IContentCatalog
    {
        public List<ContentItem> Items { get; } = [new(other, ContentSource.ProvaiEVede, "Pendente anterior", new DateOnly(2026, 10, 24),
            new Uri("https://files.test/other.mp4"), [new MediaAsset("other", new Uri("https://files.test/other.mp4"), "other.mp4", null, null)])];
        public Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItem>>(Items.ToArray());
        public Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default)
        {
            foreach (var item in items) { Items.RemoveAll(old => old.Id == item.Id); Items.Add(item); }
            return Task.CompletedTask;
        }
    }

    private sealed class Downloader : IContentDownloadService
    {
        public List<string> Ids { get; } = [];
        public Task<ContentItem> DownloadAsync(ContentItem item, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Ids.Add(item.Id);
            return Task.FromResult(item with { SyncState = SyncState.Ready });
        }
    }
}
