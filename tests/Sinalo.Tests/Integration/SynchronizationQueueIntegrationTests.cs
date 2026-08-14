using Sinalo.Application.Configuration;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.Tests.Integration;

public sealed class SynchronizationQueueIntegrationTests
{
    [Fact]
    public async Task Queue_ShouldKeepDownloadsFromDifferentSourcesOutOfTheSameExecutionWindow()
    {
        var activeExecutions = 0;
        var highestConcurrentExecutions = 0;
        var queue = new SynchronizationQueue(async (_, progress, cancellationToken) =>
        {
            highestConcurrentExecutions = Math.Max(highestConcurrentExecutions, Interlocked.Increment(ref activeExecutions));
            progress.Report(new SynchronizationQueueProgress("Baixando", 50));
            await Task.Delay(25, cancellationToken);
            Interlocked.Decrement(ref activeExecutions);
            return new SynchronizationQueueCompletion(1);
        });

        queue.Enqueue(Request(ContentSource.Missions));
        queue.Enqueue(Request(ContentSource.ProvaiEVede));
        queue.Enqueue(Request(ContentSource.Health));
        await queue.WhenIdleAsync();

        Assert.Equal(1, highestConcurrentExecutions);
        Assert.All(queue.GetSnapshot().Entries, entry => Assert.Equal(SynchronizationQueueState.Completed, entry.State));
    }

    private static SynchronizationQueueRequest Request(ContentSource source) => new(new SourceConfiguration(source, source.ToString(), "https://example.test/", AvailabilityPolicy.RollingSaturday));
}
