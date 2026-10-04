namespace Sinalo.Application.Playback;

public sealed class PlaybackActivityGate
{
    private readonly object _gate = new();
    private TaskCompletionSource _idle = Completed();
    private bool _isActive;
    public event Action<bool>? Changed;
    public bool IsActive { get { lock (_gate) return _isActive; } }

    public void SetActive(bool active)
    {
        lock (_gate)
        {
            if (_isActive == active) return;
            _isActive = active;
            if (active) _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            else _idle.TrySetResult();
        }
        Changed?.Invoke(active);
    }

    public Task WaitUntilIdleAsync(CancellationToken cancellationToken = default)
    {
        Task waiting;
        lock (_gate) waiting = _idle.Task;
        return waiting.WaitAsync(cancellationToken);
    }

    private static TaskCompletionSource Completed()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
