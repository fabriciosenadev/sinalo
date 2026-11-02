using System.IO;
using Sinalo.Application.LouvorJa;
using Sinalo.App.ViewModels;

namespace Sinalo.Tests.Integration;

public sealed class LouvorJaSessionTests
{
    private static readonly LouvorJaConfiguration Configuration = new(@"C:\Test\Louvor JA.exe", 7071);
    private sealed class ProcessMonitor : ILouvorJaProcessMonitor
    {
        public volatile LouvorJaProcessObservation Observation = new(LouvorJaProcessState.Running, 10);
        public bool StartRemainsClosed;
        public bool StartFails;
        public int Starts;
        public Task<LouvorJaProcessObservation> InspectAsync(string executable, CancellationToken token = default)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(Observation); }
        public Task StartAsync(string executable, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Starts++;
            if (StartFails) throw new IOException("Falha ao abrir o executável");
            if (!StartRemainsClosed) Observation = new(LouvorJaProcessState.Running, 11);
            return Task.CompletedTask;
        }
    }
    private sealed class Client : ILouvorJaClient
    {
        public int Pings, Reads, Commands, Searches;
        public bool Fail;
        public int FailFirst;
        public TaskCompletionSource? Block;
        public int Active, MaximumConcurrency;
        public Func<Task<IReadOnlyList<LouvorJaSong>>>? DeferredSearch;
        public Task ConnectAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Interlocked.Increment(ref Pings);
            if (Fail || Pings <= FailFirst) throw new IOException("Servidor ainda indisponível");
            return Task.CompletedTask;
        }
        public async Task<LouvorJaState> GetStateAsync(CancellationToken token = default)
        {
            var active = Interlocked.Increment(ref Active); MaximumConcurrency = Math.Max(MaximumConcurrency, active);
            try
            {
                Interlocked.Increment(ref Reads);
                if (Block is not null) await Block.Task.WaitAsync(token);
                token.ThrowIfCancellationRequested(); return new("idle", "", "", 0, 0, true, 0);
            }
            finally { Interlocked.Decrement(ref Active); }
        }
        public Task<IReadOnlyList<LouvorJaSong>> SearchAsync(string query, CancellationToken token = default)
        { token.ThrowIfCancellationRequested(); Searches++; return DeferredSearch?.Invoke() ?? Task.FromResult<IReadOnlyList<LouvorJaSong>>([new(1, "Teste", "Teste", true)]); }
        public Task OpenAsync(int id, LouvorJaMode mode, CancellationToken token = default) { Commands++; return Task.CompletedTask; }
        public Task SendAsync(LouvorJaCommand command, int volume = 50, CancellationToken token = default) { Commands++; return Task.CompletedTask; }
    }
    private static LouvorJaSession Create(ProcessMonitor processes, Client client) => new(processes, _ => client)
    { PollInterval = TimeSpan.FromMilliseconds(10), ReadinessTimeout = TimeSpan.FromSeconds(1) };
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    [Fact]
    public async Task Startup_Closed_DoesNotLaunchOrCallServer_ExternalOpeningConnects()
    {
        var process = new ProcessMonitor { Observation = new(LouvorJaProcessState.Closed) }; var client = new Client();
        await using var session = Create(process, client);
        await session.StartAsync(Configuration);
        Assert.Equal(LouvorJaConnectionState.Closed, session.Current.Connection);
        Assert.Equal(0, process.Starts); Assert.Equal(0, client.Pings);
        process.Observation = new(LouvorJaProcessState.Running, 15);
        await UntilAsync(() => session.Current.Connection == LouvorJaConnectionState.Connected);
        Assert.Equal(0, process.Starts); Assert.Equal(0, client.Commands);
    }

    [Fact]
    public async Task Startup_Running_WaitsForReadiness_WithoutLaunchingOrPlaying()
    {
        var process = new ProcessMonitor(); var client = new Client { FailFirst = 2 };
        await using var session = Create(process, client);
        var states = new List<LouvorJaConnectionState>(); session.Changed += snapshot => states.Add(snapshot.Connection);
        await session.StartAsync(Configuration);
        Assert.Contains(LouvorJaConnectionState.Waiting, states);
        Assert.Equal(LouvorJaConnectionState.Connected, session.Current.Connection);
        Assert.True(client.Pings >= 3); Assert.Equal(0, process.Starts); Assert.Equal(0, client.Commands);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Open_ReusesRunningInstance_OrLaunchesOnce_AndConnects(bool running)
    {
        var process = new ProcessMonitor { Observation = new(running ? LouvorJaProcessState.Running : LouvorJaProcessState.Closed, running ? 10 : null) };
        var client = new Client(); await using var session = Create(process, client);
        session.Configure(Configuration); await session.OpenAndConnectAsync();
        Assert.Equal(LouvorJaConnectionState.Connected, session.Current.Connection);
        Assert.Equal(running ? 0 : 1, process.Starts); Assert.Equal(0, client.Commands);
        await session.OpenAndConnectAsync(); Assert.Equal(running ? 0 : 1, process.Starts);
    }

    [Fact]
    public async Task ServerUnavailable_TimesOut_ThenRequiresExplicitRetry()
    {
        var process = new ProcessMonitor(); var client = new Client { Fail = true };
        await using var session = Create(process, client);
        await session.StartAsync(Configuration);
        Assert.Equal(LouvorJaConnectionState.Unavailable, session.Current.Connection);
        Assert.Contains("Transmissão", session.Current.Message);
        var pings = client.Pings; client.Fail = false;
        await Task.Delay(60); Assert.Equal(pings, client.Pings);
        await session.ConnectAsync(); Assert.Equal(LouvorJaConnectionState.Connected, session.Current.Connection);
    }

    [Fact]
    public async Task Closure_ClearsPresentation_EvenWhenPanelHidden_AndRestartReconnects()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        await session.StartAsync(Configuration); var reads = client.Reads;
        await UntilAsync(() => client.Pings >= 3);
        Assert.Equal(reads, client.Reads); // Hidden workspace: only identity/health ping, no content polling.
        process.Observation = new(LouvorJaProcessState.Closed);
        await UntilAsync(() => session.Current.Connection == LouvorJaConnectionState.Closed);
        Assert.Null(session.Current.Presentation);
        process.Observation = new(LouvorJaProcessState.Running, 20);
        await UntilAsync(() => session.Current.Connection == LouvorJaConnectionState.Connected);
        session.ObserveContent = true;
        await UntilAsync(() => client.Reads > reads + 1);
        Assert.Equal(0, client.Commands);
    }

    [Fact]
    public async Task CommunicationFailure_IsNotReportedAsClosed()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        await session.StartAsync(Configuration); client.Fail = true;
        await UntilAsync(() => session.Current.Connection == LouvorJaConnectionState.Lost);
        Assert.Null(session.Current.Presentation); Assert.Contains("processo continua aberto", session.Current.Message);
    }

    [Fact]
    public async Task ManualDisconnect_SuppressesAutomaticReconnect_AcrossProcessRestart()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        await session.StartAsync(Configuration); session.Disconnect(); var pings = client.Pings;
        process.Observation = new(LouvorJaProcessState.Closed); await Task.Delay(35);
        process.Observation = new(LouvorJaProcessState.Running, 30); await Task.Delay(35);
        Assert.Equal(pings, client.Pings);
        Assert.Equal(LouvorJaConnectionState.ManualDisconnected, session.Current.Connection);
        Assert.Null(session.Current.Presentation);
        await session.OpenAndConnectAsync(); Assert.Equal(LouvorJaConnectionState.Connected, session.Current.Connection);
        Assert.Equal(0, process.Starts);
    }

    [Fact]
    public async Task UnknownProcess_DoesNotLaunchDuplicateOrContactAnUnverifiedServer()
    {
        var process = new ProcessMonitor { Observation = new(LouvorJaProcessState.Unknown, Detail: "Sem permissão") };
        var client = new Client(); await using var session = Create(process, client);
        session.Configure(Configuration); await session.OpenAndConnectAsync();
        Assert.Equal(LouvorJaConnectionState.Unknown, session.Current.Connection);
        Assert.Equal(0, process.Starts); Assert.Equal(0, client.Pings);
    }

    [Fact]
    public async Task Open_ProcessNeverStarts_TimesOutWithoutHttpOrAdditionalLaunch()
    {
        var process = new ProcessMonitor { Observation = new(LouvorJaProcessState.Closed), StartRemainsClosed = true };
        var client = new Client(); await using var session = Create(process, client);
        session.Configure(Configuration); await session.OpenAndConnectAsync();
        Assert.Equal(LouvorJaConnectionState.Closed, session.Current.Connection);
        Assert.Equal(1, process.Starts); Assert.Equal(0, client.Pings);
    }

    [Fact]
    public async Task Open_ProcessClosesDuringReadiness_StopsWaiting_AndLaunchFailureIsVisible()
    {
        var process = new ProcessMonitor(); var client = new Client { Fail = true };
        await using var session = Create(process, client);
        session.Configure(Configuration); var opening = session.OpenAndConnectAsync();
        await UntilAsync(() => client.Pings > 0);
        process.Observation = new(LouvorJaProcessState.Closed); await opening;
        Assert.Equal(LouvorJaConnectionState.Closed, session.Current.Connection);
        process.StartFails = true;
        await Assert.ThrowsAsync<IOException>(() => session.OpenAndConnectAsync());
        Assert.Equal(LouvorJaConnectionState.Unavailable, session.Current.Connection);
        Assert.Contains("executável", session.Current.Message);
    }

    [Fact]
    public async Task UnverifiedOrInvalidSettings_DoNotEnableCommands()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        Assert.Throws<ArgumentException>(() => session.Configure(Configuration with { Port = 0 }));
        Assert.Throws<ArgumentException>(() => session.Configure(Configuration with { ExecutablePath = "" }));
        session.Configure(Configuration);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SearchAsync("Teste"));
        Assert.Equal(0, client.Searches); Assert.Equal(0, client.Commands);
        var observation = await new Sinalo.Infrastructure.LouvorJa.WindowsLouvorJaProcessMonitor().InspectAsync(@"C:\Test\other.exe");
        Assert.Equal(LouvorJaProcessState.Unknown, observation.State);
    }

    [Fact]
    public async Task ConfigurationChange_CancelsOldResponse_AndDoesNotReconnectWithoutApproval()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        await session.StartAsync(Configuration);
        client.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = session.GetStateAsync(); await UntilAsync(() => client.Active > 0);
        session.InvalidateConfiguration(); client.Block.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.Null(session.Current.Presentation);
        session.Configure(Configuration with { Port = 7072 }); var pings = client.Pings;
        await Task.Delay(50); Assert.Equal(pings, client.Pings);
        await session.ConnectAsync(); Assert.Equal(LouvorJaConnectionState.Connected, session.Current.Connection);
    }

    [Fact]
    public async Task Commands_AreSerialized_AndStopCancelsPendingRequests()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        await session.StartAsync(Configuration);
        await session.SearchAsync("Teste"); await session.OpenAsync(1, LouvorJaMode.Sung); await session.SendAsync(LouvorJaCommand.Volume, 40);
        Assert.Equal(2, client.Commands);
        client.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = session.GetStateAsync(); var second = session.GetStateAsync();
        await UntilAsync(() => client.Active > 0); session.Stop();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Equal(1, client.MaximumConcurrency);
        Assert.Throws<ObjectDisposedException>(() => session.Configure(Configuration));
    }

    private sealed class ConfigurationStore : ILouvorJaConfigurationStore
    {
        public Task<LouvorJaConfiguration?> LoadAsync(CancellationToken token = default) => Task.FromResult<LouvorJaConfiguration?>(Configuration);
        public Task SaveAsync(LouvorJaConfiguration configuration, CancellationToken token = default) => Task.CompletedTask;
        public Task SetEnabledAsync(bool enabled, CancellationToken token = default) => Task.CompletedTask;
    }
    [Fact]
    public async Task CancelledSearchFailure_DoesNotDisconnectHealthySession()
    {
        var response = new TaskCompletionSource<IReadOnlyList<LouvorJaSong>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { DeferredSearch = () => response.Task };
        await using var session = Create(new ProcessMonitor(), client);
        await session.StartAsync(Configuration);
        using var cancellation = new CancellationTokenSource();
        var search = session.SearchAsync("antiga", cancellation.Token);
        await UntilAsync(() => client.Searches > 0);
        cancellation.Cancel(); response.SetException(new IOException("Resposta antiga cancelada"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
        Assert.Equal(LouvorJaConnectionState.Connected, session.Current.Connection);
    }
    [Fact]
    public async Task ViewModel_AutomaticStartup_ManualDisconnect_AndOpenFlow()
    {
        var process = new ProcessMonitor(); var client = new Client();
        await using var session = Create(process, client);
        using var model = new LouvorJaIntegrationViewModel(session, new ConfigurationStore());
        await model.InitializeSessionAsync();
        Assert.True(model.IsConnected); Assert.Contains("pronto", model.ConnectionLabel);
        model.Query = "Teste"; await model.SearchCommand.ExecuteAsync(null); Assert.Single(model.Songs);
        model.DisconnectCommand.Execute(null);
        Assert.False(model.IsConnected); Assert.Null(model.State); Assert.Single(model.Songs);
        Assert.Contains("operador", model.ConnectionLabel);
        await model.LaunchCommand.ExecuteAsync(null); Assert.True(model.IsConnected); Assert.False(model.IsWorking);
        process.Observation = new(LouvorJaProcessState.Closed);
        await UntilAsync(() => !model.IsConnected); Assert.Null(model.State); Assert.False(model.CanControl);
    }

    [Fact]
    public async Task ViewModel_Waiting_IsVisibleAndCancellable_WithoutLaunch()
    {
        var process = new ProcessMonitor(); var client = new Client { Fail = true };
        await using var session = Create(process, client);
        using var model = new LouvorJaIntegrationViewModel(session, new ConfigurationStore());
        var startup = model.InitializeSessionAsync();
        await UntilAsync(() => model.ConnectionState == LouvorJaConnectionState.Waiting);
        Assert.True(model.IsWorking); Assert.False(model.CanConnect); Assert.True(model.CancelCommand.CanExecute(null));
        model.CancelCommand.Execute(null); await startup;
        Assert.False(model.IsWorking); Assert.Equal(0, process.Starts);
        Assert.Equal(LouvorJaConnectionState.ManualDisconnected, model.ConnectionState);
    }
}
