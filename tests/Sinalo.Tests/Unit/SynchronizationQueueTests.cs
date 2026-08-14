using Sinalo.Application.Configuration;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using System.Net.Http;

namespace Sinalo.Tests.Unit;

public sealed class SynchronizationQueueTests
{
    [Fact]
    public async Task Queue_ShouldRunRequestsOneAtATimeInInsertionOrder()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<ContentSource>();
        var running = 0;
        var maximumRunning = 0;
        var queue = new SynchronizationQueue(async (request, _, _) =>
        {
            maximumRunning = Math.Max(maximumRunning, Interlocked.Increment(ref running));
            calls.Add(request.Configuration.Source);
            if (request.Configuration.Source == ContentSource.Missions)
            {
                started.TrySetResult();
                await releaseFirst.Task;
            }
            Interlocked.Decrement(ref running);
            return new SynchronizationQueueCompletion(1);
        });

        Assert.True(queue.Enqueue(Request(ContentSource.Missions)).Added);
        await started.Task;
        Assert.True(queue.Enqueue(Request(ContentSource.Health)).Added);
        releaseFirst.TrySetResult();
        await queue.WhenIdleAsync();

        Assert.Equal([ContentSource.Missions, ContentSource.Health], calls);
        Assert.Equal(1, maximumRunning);
    }

    [Fact]
    public async Task Queue_ShouldRejectTheSameSourceWhileItIsPending()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new SynchronizationQueue(async (_, _, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return new SynchronizationQueueCompletion(1);
        });

        Assert.True(queue.Enqueue(Request(ContentSource.ProvaiEVede)).Added);
        await started.Task;

        var duplicate = queue.Enqueue(Request(ContentSource.ProvaiEVede));

        Assert.False(duplicate.Added);
        Assert.Contains("já está", duplicate.Message);
        release.TrySetResult();
        await queue.WhenIdleAsync();
    }

    [Fact]
    public async Task Queue_ShouldCancelTheCurrentAndWaitingRequests()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new SynchronizationQueue(async (_, _, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new SynchronizationQueueCompletion(0);
        });

        queue.Enqueue(Request(ContentSource.Missions));
        await started.Task;
        queue.Enqueue(Request(ContentSource.Health));
        queue.CancelAll();
        await queue.WhenIdleAsync();

        var snapshot = queue.GetSnapshot();
        Assert.All(snapshot.Entries, entry => Assert.Equal(SynchronizationQueueState.Cancelled, entry.State));
        Assert.False(snapshot.IsProcessing);
    }

    [Fact]
    public async Task Queue_ShouldContinueAfterAFailedRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new SynchronizationQueue(async (request, _, _) =>
        {
            if (request.Configuration.Source == ContentSource.Missions)
            {
                started.TrySetResult();
                await release.Task;
                throw new HttpRequestException("Rede indisponível.");
            }
            return new SynchronizationQueueCompletion(2);
        });

        queue.Enqueue(Request(ContentSource.Missions));
        await started.Task;
        queue.Enqueue(Request(ContentSource.Health));
        release.TrySetResult();
        await queue.WhenIdleAsync();

        var entries = queue.GetSnapshot().Entries;
        Assert.Equal(SynchronizationQueueState.Failed, entries.Single(entry => entry.Source == ContentSource.Missions).State);
        Assert.Equal(SynchronizationQueueState.Completed, entries.Single(entry => entry.Source == ContentSource.Health).State);
        Assert.Equal(2, entries.Single(entry => entry.Source == ContentSource.Health).ReadyItems);
    }

    private static SynchronizationQueueRequest Request(ContentSource source) => new(new SourceConfiguration(source, source.ToString(), "https://example.test/", AvailabilityPolicy.RollingSaturday));
}
