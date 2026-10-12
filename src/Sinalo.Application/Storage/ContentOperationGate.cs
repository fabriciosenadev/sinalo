namespace Sinalo.Application.Storage;

/// <summary>Serializes imports, downloads, relocation and destructive file operations.</summary>
public sealed class ContentOperationGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<IDisposable> EnterAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        return new Lease(_gate);
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Release(); }
    }
}
