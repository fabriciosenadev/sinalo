using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Sinalo.Infrastructure;

public sealed class MpvCommandException(string message) : IOException(message);

/// <summary>One reader, correlated replies, and serialized writes. The stream is owned by this connection.</summary>
public sealed class MpvIpcConnection : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly Task _readerTask;
    private readonly TimeSpan _timeout;
    private long _requestId;
    private int _disposed;
    public event Action<JsonElement>? EventReceived;
    public event Action? Disconnected;
    public bool IsConnected => !_readerTask.IsCompleted && Volatile.Read(ref _disposed) == 0;

    public MpvIpcConnection(Stream stream, TimeSpan? commandTimeout = null)
    {
        _stream = stream;
        _timeout = commandTimeout ?? TimeSpan.FromSeconds(3);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        _readerTask = ReadAsync();
    }

    public async Task<JsonElement> SendAsync(object[] command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!IsConnected) throw new IOException("A conexão com o MPV foi encerrada.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(_timeout);
        var id = Interlocked.Increment(ref _requestId);
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = reply;
        try
        {
            await _writeGate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try { await _writer.WriteLineAsync(JsonSerializer.Serialize(new { command, request_id = id }).AsMemory(), timeout.Token).ConfigureAwait(false); }
            finally { _writeGate.Release(); }
            return await reply.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
        { throw new TimeoutException("O MPV não confirmou o comando a tempo."); }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task ReadAsync()
    {
        try
        {
            using var reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
            while (await reader.ReadLineAsync(_lifetime.Token).ConfigureAwait(false) is { } line)
            {
                JsonDocument document;
                try { document = JsonDocument.Parse(line); }
                catch (JsonException) { continue; }
                using (document)
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) continue;
                    if (root.TryGetProperty("request_id", out var request) && request.ValueKind == JsonValueKind.Number && request.TryGetInt64(out var id) && _pending.TryRemove(id, out var reply))
                    {
                        var error = root.TryGetProperty("error", out var status) && status.ValueKind == JsonValueKind.String ? status.GetString() : "resposta inválida";
                        if (error == "success") reply.TrySetResult(root.TryGetProperty("data", out var data) ? data.Clone() : default);
                        else reply.TrySetException(new MpvCommandException($"MPV: {error}"));
                    }
                    else if (root.TryGetProperty("event", out var eventName) && eventName.ValueKind == JsonValueKind.String) EventReceived?.Invoke(root.Clone());
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            foreach (var pair in _pending) pair.Value.TrySetException(new IOException("A conexão com o MPV foi encerrada."));
            _pending.Clear();
            if (Volatile.Read(ref _disposed) == 0) Disconnected?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _stream.Dispose();
        await _readerTask.ConfigureAwait(false);
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try { try { _writer.Dispose(); } catch (IOException) { } catch (ObjectDisposedException) { } }
        finally { _writeGate.Release(); }
    }
}
