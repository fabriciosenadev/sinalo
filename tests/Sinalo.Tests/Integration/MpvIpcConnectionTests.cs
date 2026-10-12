using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class MpvIpcConnectionTests
{
    [Fact]
    public async Task CorrelatesConcurrentRepliesAndInterleavedEvents()
    {
        await using var pair = await Pair.CreateAsync();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Connection.EventReceived += message => received.TrySetResult(message.GetProperty("event").GetString()!);
        var first = pair.Connection.SendAsync(["get_property", "duration"]);
        var second = pair.Connection.SendAsync(["get_property", "volume"]);
        var one = await pair.ReadAsync();
        var two = await pair.ReadAsync();
        await pair.Writer.WriteLineAsync("not-json");
        await pair.Writer.WriteLineAsync("[]");
        await pair.Writer.WriteLineAsync("{\"event\":\"file-loaded\"}");
        await pair.ReplyAsync(two, "success", 42);
        await pair.ReplyAsync(one, "success", 8);
        Assert.Equal(8, (await first).GetInt32());
        Assert.Equal(42, (await second).GetInt32());
        Assert.Equal("file-loaded", await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ErrorReplyDoesNotPreventNextCommand()
    {
        await using var pair = await Pair.CreateAsync();
        var command = pair.Connection.SendAsync(["bad_command"]);
        await pair.ReplyAsync(await pair.ReadAsync(), "command not found");
        await Assert.ThrowsAsync<MpvCommandException>(() => command);
        var next = pair.Connection.SendAsync(["stop"]);
        await pair.ReplyAsync(await pair.ReadAsync(), "success");
        Assert.Equal(JsonValueKind.Null, (await next).ValueKind);
    }

    [Fact]
    public async Task TimesOutAndIgnoresLateReply()
    {
        await using var pair = await Pair.CreateAsync(TimeSpan.FromMilliseconds(150));
        var command = pair.Connection.SendAsync(["get_property", "duration"]);
        var request = await pair.ReadAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => command);
        await pair.ReplyAsync(request, "success", 1);
        var next = pair.Connection.SendAsync(["stop"]);
        await pair.ReplyAsync(await pair.ReadAsync(), "success");
        await next;
    }

    [Fact]
    public async Task CancelsCommandWithoutLosingConnection()
    {
        await using var pair = await Pair.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var command = pair.Connection.SendAsync(["get_property", "duration"], cancellation.Token);
        await pair.ReadAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
        Assert.True(pair.Connection.IsConnected);
    }

    [Fact]
    public async Task DisconnectionCompletesPendingCommands()
    {
        await using var pair = await Pair.CreateAsync();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Connection.Disconnected += () => closed.TrySetResult();
        var command = pair.Connection.SendAsync(["stop"]);
        await pair.ReadAsync();
        pair.Server.Dispose();
        await Assert.ThrowsAsync<IOException>(() => command);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pair.Connection.IsConnected);
        await Assert.ThrowsAsync<IOException>(() => pair.Connection.SendAsync(["stop"]));
    }

    [Fact]
    public async Task DisposalCancelsPendingRequestAndIsIdempotent()
    {
        await using var pair = await Pair.CreateAsync();
        var command = pair.Connection.SendAsync(["stop"]);
        await pair.ReadAsync();
        await pair.Connection.DisposeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => command);
        await pair.Connection.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pair.Connection.SendAsync(["stop"]));
    }

    private sealed class Pair : IAsyncDisposable
    {
        public required NamedPipeServerStream Server { get; init; }
        public required MpvIpcConnection Connection { get; init; }
        public required StreamReader Reader { get; init; }
        public required StreamWriter Writer { get; init; }
        public static async Task<Pair> CreateAsync(TimeSpan? timeout = null)
        {
            var name = $"sinalo-ipc-test-{Guid.NewGuid():N}";
            var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            var connection = server.WaitForConnectionAsync();
            await client.ConnectAsync(2000);
            await connection;
            return new() { Server = server, Connection = new(client, timeout), Reader = new(server, Encoding.UTF8, leaveOpen: true), Writer = new(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true } };
        }
        public async Task<long> ReadAsync()
        {
            using var message = JsonDocument.Parse((await Reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)))!);
            return message.RootElement.GetProperty("request_id").GetInt64();
        }
        public Task ReplyAsync(long id, string error, object? data = null) => Writer.WriteLineAsync(JsonSerializer.Serialize(new { request_id = id, error, data }));
        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            Reader.Dispose();
            try { Writer.Dispose(); } catch (IOException) { } catch (ObjectDisposedException) { }
            Server.Dispose();
        }
    }
}
