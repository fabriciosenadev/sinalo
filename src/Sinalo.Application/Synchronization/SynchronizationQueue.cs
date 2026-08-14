using Sinalo.Application.Configuration;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public enum SynchronizationQueueState
{
    Waiting,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record SynchronizationQueueRequest(SourceConfiguration Configuration);
public sealed record SynchronizationQueueProgress(string Message, double? Percentage = null);
public sealed record SynchronizationQueueCompletion(int ReadyItems);
public sealed record SynchronizationQueueEntry(
    ContentSource Source,
    string SourceName,
    SynchronizationQueueState State,
    string Message,
    double? Percentage,
    int ReadyItems = 0);
public sealed record SynchronizationQueueSnapshot(bool IsProcessing, IReadOnlyList<SynchronizationQueueEntry> Entries);
public sealed record SynchronizationQueueEnqueueResult(bool Added, string Message);

public sealed class SynchronizationQueue(
    Func<SynchronizationQueueRequest, IProgress<SynchronizationQueueProgress>, CancellationToken, Task<SynchronizationQueueCompletion>> executor)
{
    private readonly object _gate = new();
    private readonly List<QueueJob> _jobs = [];
    private CancellationTokenSource? _currentCancellation;
    private TaskCompletionSource _idleCompletion = CompletedSource();
    private bool _isProcessing;

    public event Action<SynchronizationQueueSnapshot>? Changed;

    public SynchronizationQueueEnqueueResult Enqueue(SynchronizationQueueRequest request)
    {
        var shouldStart = false;
        SynchronizationQueueEnqueueResult result;
        lock (_gate)
        {
            _jobs.RemoveAll(job => job.State is SynchronizationQueueState.Completed or SynchronizationQueueState.Failed or SynchronizationQueueState.Cancelled);
            if (_jobs.Any(job => job.Request.Configuration.Source == request.Configuration.Source))
            {
                return new(false, $"{request.Configuration.DisplayName} já está em andamento ou na fila.");
            }

            _jobs.Add(new QueueJob(request));
            result = new(true, $"{request.Configuration.DisplayName} foi adicionado à fila.");
            if (!_isProcessing)
            {
                _isProcessing = true;
                _idleCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                shouldStart = true;
            }
        }

        Publish();
        if (shouldStart) _ = ProcessAsync();
        return result;
    }

    public void CancelAll()
    {
        lock (_gate)
        {
            foreach (var job in _jobs.Where(job => job.State == SynchronizationQueueState.Waiting))
            {
                job.State = SynchronizationQueueState.Cancelled;
                job.Message = "Cancelada pelo operador.";
            }
            _currentCancellation?.Cancel();
        }
        Publish();
    }

    public Task WhenIdleAsync()
    {
        lock (_gate) return _idleCompletion.Task;
    }

    public SynchronizationQueueSnapshot GetSnapshot()
    {
        lock (_gate) return CreateSnapshot();
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            QueueJob? job;
            lock (_gate)
            {
                job = _jobs.FirstOrDefault(candidate => candidate.State == SynchronizationQueueState.Waiting);
                if (job is null)
                {
                    _isProcessing = false;
                    _currentCancellation?.Dispose();
                    _currentCancellation = null;
                    _idleCompletion.TrySetResult();
                    break;
                }

                job.State = SynchronizationQueueState.Running;
                job.Message = "Consultando a fonte oficial...";
                _currentCancellation = new CancellationTokenSource();
            }

            Publish();
            var cancellation = _currentCancellation;
            var progress = new Progress<SynchronizationQueueProgress>(update => UpdateProgress(job, update));
            try
            {
                var completion = await executor(job.Request, progress, cancellation.Token);
                lock (_gate)
                {
                    job.State = SynchronizationQueueState.Completed;
                    job.ReadyItems = completion.ReadyItems;
                    job.Percentage = 100;
                    job.Message = completion.ReadyItems > 0
                        ? $"{completion.ReadyItems} vídeo(s) disponíveis offline."
                        : "Nenhum vídeo novo estava disponível.";
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                lock (_gate)
                {
                    job.State = SynchronizationQueueState.Cancelled;
                    job.Message = "Cancelada pelo operador.";
                }
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    job.State = SynchronizationQueueState.Failed;
                    job.Message = exception.Message;
                }
            }
            finally
            {
                cancellation.Dispose();
                lock (_gate)
                {
                    if (ReferenceEquals(_currentCancellation, cancellation)) _currentCancellation = null;
                }
                Publish();
            }
        }
        Publish();
    }

    private void UpdateProgress(QueueJob job, SynchronizationQueueProgress update)
    {
        lock (_gate)
        {
            if (job.State != SynchronizationQueueState.Running) return;
            job.Message = update.Message;
            job.Percentage = update.Percentage;
        }
        Publish();
    }

    private void Publish()
    {
        SynchronizationQueueSnapshot snapshot;
        lock (_gate)
        {
            snapshot = CreateSnapshot();
        }
        Changed?.Invoke(snapshot);
    }

    private SynchronizationQueueSnapshot CreateSnapshot() => new(_isProcessing, _jobs.Select(job => new SynchronizationQueueEntry(
        job.Request.Configuration.Source,
        job.Request.Configuration.DisplayName,
        job.State,
        job.Message,
        job.Percentage,
        job.ReadyItems)).ToArray());

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    private sealed class QueueJob(SynchronizationQueueRequest request)
    {
        public SynchronizationQueueRequest Request { get; } = request;
        public SynchronizationQueueState State { get; set; } = SynchronizationQueueState.Waiting;
        public string Message { get; set; } = "Aguardando na fila.";
        public double? Percentage { get; set; }
        public int ReadyItems { get; set; }
    }
}
