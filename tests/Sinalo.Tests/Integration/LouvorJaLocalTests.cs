using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Sinalo.Application.LouvorJa;
using Sinalo.App.ViewModels;
using Sinalo.Infrastructure.LouvorJa;

namespace Sinalo.Tests.Integration;

public sealed class LouvorJaLocalTests
{
    [Fact]
    public async Task RealSocketTransport_UsesOnlyIsolatedEphemeralLoopbackServer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = new List<string>();
        string[] responses =
        ["""{"status":"ok","app":"LouvorJA","running":true}""",
         """{"status":"ok","data":{"type":"idle","updatedAt":1}}""",
         """{"status":"ok","results":[{"id_music":11,"name":"Hino de teste","artist":"Teste","has_instrumental":true}]}""",
         """{"status":"ok"}""",
         """{"status":"ok","data":{"type":"media","title":"Hino de teste","currentLyric":"Teste","slideIndex":0,"totalSlides":2,"isPaused":false,"volume":60}}""",
         """{"status":"ok","data":{"type":"bible","bibleReference":"João 3:16","bibleText":"Texto de teste"}}"""];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            foreach (var body in responses)
            {
                using var socket = await listener.AcceptTcpClientAsync(deadline.Token);
                await using var stream = socket.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                requests.Add((await reader.ReadLineAsync(deadline.Token))!);
                while (await reader.ReadLineAsync(deadline.Token) is { Length: > 0 }) { }
                var payload = Encoding.UTF8.GetBytes(body);
                var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers, deadline.Token); await stream.WriteAsync(payload, deadline.Token);
            }
        }, deadline.Token);
        using var connection = new LouvorJaLocalConnection();
        var client = connection.Create(port);
        await client.ConnectAsync(deadline.Token);
        Assert.Equal("idle", (await client.GetStateAsync(deadline.Token)).Type);
        var found = await client.SearchAsync("hino & paz", deadline.Token);
        Assert.Equal(11, Assert.Single(found).Id);
        await client.OpenAsync(11, LouvorJaMode.Instrumental, deadline.Token);
        Assert.Equal(60, (await client.GetStateAsync(deadline.Token)).Volume);
        Assert.Equal("bible", (await client.GetStateAsync(deadline.Token)).Type);
        await server;
        Assert.Contains("q=hino%20%26%20paz", requests[2]);
        Assert.Contains("id=11&tag=2", requests[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => connection.Create(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => connection.Create(65536));
    }

    [Theory]
    [InlineData("https://127.0.0.1:7071/api/ping")]
    [InlineData("http://localhost:7071/api/ping")]
    [InlineData("http://192.168.1.10:7071/api/ping")]
    [InlineData("http://example.com/api/ping")]
    [InlineData("http://user@127.0.0.1:7071/api/ping")]
    public async Task RestrictedTransport_RejectsOtherDestinationsBeforeSending(string uri)
    {
        var sink = new CountingHandler();
        using var http = new HttpClient(new LouvorJaLoopbackHandler(sink));
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(uri));
        Assert.Equal(0, sink.Requests);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requests++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)); }
    }

    [Fact]
    public async Task RealMode_UsesFactoryPortGuardsLaunchAndObservesExternalChanges()
    {
        var handler = new LouvorJaMockHandler { Latency = TimeSpan.Zero };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://test.invalid/") };
        var client = new LouvorJaHttpClient(http);
        var ports = new List<int>(); var launches = 0; string? conflict = "Pare o vídeo local.";
        using var vm = new LouvorJaIntegrationViewModel(p => { ports.Add(p); return client; }, (_, _) => { launches++; return Task.CompletedTask; }, () => conflict);
        Assert.True(vm.IsReal); Assert.False(vm.IsSimulation);
        Assert.DoesNotContain("SIMULADO", vm.SafetyNotice);
        await vm.LaunchCommand.ExecuteAsync(null); Assert.Equal(1, launches);
        vm.PortText = "abc"; await vm.ConnectCommand.ExecuteAsync(null); Assert.False(vm.IsConnected);
        vm.PortText = "7777"; await vm.ConnectCommand.ExecuteAsync(null); Assert.True(vm.IsConnected);
        Assert.Equal(7777, ports[^1]);
        vm.Query = "teste"; await vm.SearchCommand.ExecuteAsync(null); vm.SelectedSong = vm.Songs[0];
        await vm.OpenCommand.ExecuteAsync(null); Assert.Equal("Pare o vídeo local.", vm.Message);
        Assert.Equal("idle", (await client.GetStateAsync()).Type);
        conflict = null; await vm.OpenCommand.ExecuteAsync(null); Assert.True(vm.CanControl);
        await client.SendAsync(LouvorJaCommand.TogglePlayback);
        await vm.ObserveAsync(); Assert.True(vm.State!.IsPaused);
        conflict = "Pare a apresentação."; await vm.ToggleCommand.ExecuteAsync(null); Assert.True(vm.State.IsPaused);
        conflict = null; await vm.ToggleCommand.ExecuteAsync(null); Assert.False(vm.State.IsPaused);
        vm.RequestedVolume = 64; await vm.ApplyVolumeCommand.ExecuteAsync(null); Assert.Equal(64, vm.State.Volume);
        await vm.NextCommand.ExecuteAsync(null); await vm.PreviousCommand.ExecuteAsync(null);
        vm.DisconnectCommand.Execute(null); Assert.False(vm.IsConnected);
        Assert.Equal("media", (await client.GetStateAsync()).Type); // disconnect never stops music
        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.CloseCommand.ExecuteAsync(null); Assert.False(vm.CanControl);
        handler.Scenario = LouvorJaScenario.Unavailable;
        await vm.ObserveAsync(); Assert.False(vm.IsConnected); Assert.Contains("Conexão perdida", vm.Message);
    }

    [Fact]
    public async Task RealMode_NoEffectOnlyRetriesObservationNeverTheCommand()
    {
        var handler = new LouvorJaMockHandler { Scenario = LouvorJaScenario.NoEffect, Latency = TimeSpan.Zero };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://test.invalid/") };
        var logs = new List<string>(); var client = new LouvorJaHttpClient(http, logs.Add);
        using var vm = new LouvorJaIntegrationViewModel(_ => client, (_, _) => Task.CompletedTask);
        await vm.ConnectCommand.ExecuteAsync(null); vm.Query = "teste";
        await vm.SearchCommand.ExecuteAsync(null); vm.SelectedSong = vm.Songs[0];
        await vm.OpenCommand.ExecuteAsync(null);
        Assert.Contains("não foi confirmada", vm.Message);
        Assert.Single(logs, x => x.StartsWith("GET api/open-song"));
        Assert.True(logs.Count(x => x == "GET api/status") > 2);
    }

    [Fact]
    public async Task Launcher_RejectsMissingOrOtherExecutablesWithoutStartingAnything()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => LouvorJaApplicationLauncher.StartAsync(@"C:\missing\Louvor JA.exe"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => LouvorJaApplicationLauncher.StartAsync(@"C:\Windows\System32\notepad.exe"));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LouvorJaApplicationLauncher.StartAsync("missing", cancelled.Token));
    }
}
