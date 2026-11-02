namespace Sinalo.Application.LouvorJa;

/// <summary>One session per Sinalo window, independent of navigation. Never sends music commands automatically.</summary>
public sealed class LouvorJaSession(ILouvorJaProcessMonitor processes, Func<int, ILouvorJaClient> clientFactory) : ILouvorJaSession
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource _epoch = new();
    private LouvorJaConfiguration? _configuration;
    private ILouvorJaClient? _client;
    private int _revision;
    private int _clientRevision = -1;
    private bool _valid;
    private bool _manual;
    private bool _stopped;
    private bool _armed;
    private readonly List<CancellationTokenSource> _retired = [];
    private Task? _monitor;
    private int? _lastProcessId;
    private LouvorJaProcessState _lastProcess = LouvorJaProcessState.Unknown;
    private LouvorJaConnectionSnapshot _current = new(0, LouvorJaConnectionState.Disconnected, "Conexão ainda não verificada.");
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public bool ObserveContent { get; set; }
    public LouvorJaConnectionSnapshot Current { get { lock (_sync) return _current; } }
    public event Action<LouvorJaConnectionSnapshot>? Changed;

    private void Publish(LouvorJaConnectionState state, string message, int revision, LouvorJaState? content = null)
    {
        LouvorJaConnectionSnapshot snapshot;
        lock (_sync)
        {
            if (_stopped || revision != _revision) return;
            snapshot = _current = new(_current.Sequence + 1, state, message, content);
        }
        Changed?.Invoke(snapshot);
    }
    private CancellationTokenSource AdvanceEpoch()
    {
        var old = _epoch; _epoch = new(); _revision++;
        _retired.Add(old);
        return old;
    }
    public void Configure(LouvorJaConfiguration configuration)
    {
        if (configuration.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(configuration.ExecutablePath)) throw new ArgumentException("Confira o caminho e a porta do Louvor JA.");
        int revision; CancellationTokenSource previous;
        lock (_sync)
        {
            if (_stopped) throw new ObjectDisposedException(nameof(LouvorJaSession));
            if (_valid && _configuration == configuration) return;
            previous = AdvanceEpoch(); _configuration = configuration; _valid = true; _armed = false;
            _lastProcess = LouvorJaProcessState.Unknown; _lastProcessId = null; revision = _revision;
        }
        previous.Cancel();
        Publish(_manual ? LouvorJaConnectionState.ManualDisconnected : LouvorJaConnectionState.Disconnected, "Configuração alterada. Teste a conexão novamente.", revision);
    }
    public void InvalidateConfiguration()
    {
        int revision; CancellationTokenSource previous;
        lock (_sync) { if (_stopped) return; previous = AdvanceEpoch(); _valid = false; _armed = false; revision = _revision; }
        previous.Cancel();
        Publish(LouvorJaConnectionState.Disconnected, "Configuração alterada. Confira e teste a conexão novamente.", revision);
    }
    public void Disconnect()
    {
        int revision; CancellationTokenSource previous;
        lock (_sync) { if (_stopped) return; _manual = true; previous = AdvanceEpoch(); revision = _revision; }
        previous.Cancel();
        Publish(LouvorJaConnectionState.ManualDisconnected, "Desconectado pelo operador. Reconexão automática suspensa nesta sessão; a música não foi interrompida.", revision);
    }
    private (int Revision, LouvorJaConfiguration Configuration, CancellationToken Epoch) Capture()
    {
        lock (_sync)
        {
            if (_stopped) throw new ObjectDisposedException(nameof(LouvorJaSession));
            if (!_valid || _configuration is null) throw new InvalidOperationException("Confira o caminho e a porta e tente conectar.");
            return (_revision, _configuration, _epoch.Token);
        }
    }
    private ILouvorJaClient Client(int revision, int port)
    {
        if (_client is null || _clientRevision != revision) { _client = clientFactory(port); _clientRevision = revision; }
        return _client;
    }
    private async Task<LouvorJaProcessObservation> InspectAsync(string executable, CancellationToken token)
    {
        var result = await processes.InspectAsync(executable, token).ConfigureAwait(false);
        _lastProcess = result.State; _lastProcessId = result.ProcessId;
        return result;
    }
    private void EnsureMonitor()
    {
        lock (_sync) { if (!_stopped) _monitor ??= MonitorAsync(); }
    }
    public async Task StartAsync(LouvorJaConfiguration configuration, CancellationToken token = default)
    {
        Configure(configuration); EnsureMonitor();
        await WaitReadyAsync(launch: false, automatic: true, token).ConfigureAwait(false);
    }
    public Task ConnectAsync(CancellationToken token = default) { EnsureMonitor(); return WaitReadyAsync(false, false, token); }
    public Task OpenAndConnectAsync(CancellationToken token = default) { EnsureMonitor(); return WaitReadyAsync(true, false, token); }

    private async Task WaitReadyAsync(bool launch, bool automatic, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var captured = Capture();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token, captured.Epoch);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            lock (_sync) { if (automatic && _manual) return; if (!automatic) _manual = false; _armed = true; }
            var process = await InspectAsync(captured.Configuration.ExecutablePath, linked.Token).ConfigureAwait(false);
            if (process.State == LouvorJaProcessState.Unknown)
            {
                Publish(LouvorJaConnectionState.Unknown, process.Detail ?? "Não foi possível verificar o processo.", captured.Revision); return;
            }
            if (process.State == LouvorJaProcessState.Closed && !launch)
            {
                Publish(LouvorJaConnectionState.Closed, "Louvor JA fechado. Use Abrir Louvor JA para iniciar e conectar.", captured.Revision); return;
            }
            Publish(LouvorJaConnectionState.Waiting, "Aguardando o Louvor JA ficar pronto para controle…", captured.Revision);
            if (process.State == LouvorJaProcessState.Closed) await processes.StartAsync(captured.Configuration.ExecutablePath, linked.Token).ConfigureAwait(false);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            deadline.CancelAfter(ReadinessTimeout);
            Exception? lastError = null;
            var seenRunning = process.State == LouvorJaProcessState.Running;
            try
            {
                while (true)
                {
                    process = await InspectAsync(captured.Configuration.ExecutablePath, deadline.Token).ConfigureAwait(false);
                    if (process.State == LouvorJaProcessState.Unknown)
                    { Publish(LouvorJaConnectionState.Unknown, process.Detail ?? "Não foi possível verificar o processo.", captured.Revision); return; }
                    if (process.State == LouvorJaProcessState.Running)
                    {
                        seenRunning = true;
                        try
                        {
                            var client = Client(captured.Revision, captured.Configuration.Port);
                            await client.ConnectAsync(deadline.Token).ConfigureAwait(false);
                            var state = await client.GetStateAsync(deadline.Token).ConfigureAwait(false);
                            deadline.Token.ThrowIfCancellationRequested();
                            Publish(LouvorJaConnectionState.Connected, "Louvor JA conectado e pronto para controle.", captured.Revision, state); return;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception error) { lastError = error; }
                    }
                    else if (!launch || seenRunning)
                    { Publish(LouvorJaConnectionState.Closed, "O Louvor JA foi fechado.", captured.Revision); return; }
                    await Task.Delay(PollInterval, deadline.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!linked.IsCancellationRequested)
            {
                Publish(process.State == LouvorJaProcessState.Closed ? LouvorJaConnectionState.Closed : LouvorJaConnectionState.Unavailable,
                    process.State == LouvorJaProcessState.Closed ? "O Louvor JA não iniciou no prazo. Confira o executável."
                        : "Louvor JA aberto, mas o servidor de controle está indisponível. Ative-o em Configurações > Transmissão e confira a porta. " + lastError?.Message,
                    captured.Revision);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { Publish(LouvorJaConnectionState.Unavailable, error.Message, captured.Revision); throw; }
        finally { _gate.Release(); }
    }

    private async Task MonitorAsync()
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_lifetime.Token).ConfigureAwait(false))
            {
                try { await CheckAsync().ConfigureAwait(false); }
                catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested) { }
                catch (Exception error)
                {
                    var revision = 0; lock (_sync) revision = _revision;
                    Publish(LouvorJaConnectionState.Unknown, "Não foi possível verificar o Louvor JA: " + error.Message, revision);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
    private async Task CheckAsync()
    {
        (int Revision, LouvorJaConfiguration Configuration, CancellationToken Epoch) captured;
        lock (_sync) { if (!_valid || !_armed || _stopped) return; captured = Capture(); }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, captured.Epoch);
        if (!await _gate.WaitAsync(0, linked.Token).ConfigureAwait(false)) return;
        var reconnect = false;
        try
        {
            var wasRunning = _lastProcess == LouvorJaProcessState.Running; var oldId = _lastProcessId;
            var process = await InspectAsync(captured.Configuration.ExecutablePath, linked.Token).ConfigureAwait(false);
            bool manual; lock (_sync) manual = _manual;
            if (manual)
            { Publish(LouvorJaConnectionState.ManualDisconnected, "Reconexão automática suspensa pelo operador. Use Tentar conectar novamente.", captured.Revision); return; }
            if (process.State == LouvorJaProcessState.Closed)
            { Publish(LouvorJaConnectionState.Closed, "O Louvor JA está fechado.", captured.Revision); return; }
            if (process.State == LouvorJaProcessState.Unknown)
            { Publish(LouvorJaConnectionState.Unknown, process.Detail ?? "Não foi possível verificar o processo.", captured.Revision); return; }
            if (!wasRunning || oldId != process.ProcessId) { reconnect = true; return; }
            if (Current.Connection != LouvorJaConnectionState.Connected) return;
            try
            {
                var client = Client(captured.Revision, captured.Configuration.Port);
                await client.ConnectAsync(linked.Token).ConfigureAwait(false);
                var state = ObserveContent ? await client.GetStateAsync(linked.Token).ConfigureAwait(false) : Current.Presentation;
                Publish(LouvorJaConnectionState.Connected, "Louvor JA conectado e pronto para controle.", captured.Revision, state);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { Publish(LouvorJaConnectionState.Lost, "Conexão perdida; o processo continua aberto. Confira o servidor e tente conectar novamente. " + error.Message, captured.Revision); }
        }
        finally
        {
            _gate.Release();
            if (reconnect) await WaitReadyAsync(false, true, linked.Token).ConfigureAwait(false);
        }
    }
    private async Task<T> ExecuteAsync<T>(Func<ILouvorJaClient, CancellationToken, Task<T>> action, CancellationToken token, bool publishContent = false)
    {
        var captured = Capture();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token, captured.Epoch);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (Current.Connection != LouvorJaConnectionState.Connected) throw new InvalidOperationException("O Louvor JA não está conectado.");
            var process = await InspectAsync(captured.Configuration.ExecutablePath, linked.Token).ConfigureAwait(false);
            if (process.State != LouvorJaProcessState.Running)
            {
                Publish(process.State == LouvorJaProcessState.Closed ? LouvorJaConnectionState.Closed : LouvorJaConnectionState.Unknown,
                    process.Detail ?? "O Louvor JA foi fechado.", captured.Revision);
                throw new InvalidOperationException("Não foi possível confirmar o processo do Louvor JA.");
            }
            var result = await action(Client(captured.Revision, captured.Configuration.Port), linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            if (publishContent && result is LouvorJaState state)
                Publish(LouvorJaConnectionState.Connected, "Louvor JA conectado e pronto para controle.", captured.Revision, state);
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            if (linked.IsCancellationRequested) throw new OperationCanceledException(linked.Token);
            if (Current.Connection == LouvorJaConnectionState.Connected) Publish(LouvorJaConnectionState.Lost, "Conexão perdida: " + error.Message, captured.Revision);
            throw;
        }
        finally { _gate.Release(); }
    }
    public Task<IReadOnlyList<LouvorJaSong>> SearchAsync(string query, CancellationToken token = default) => ExecuteAsync((client, ct) => client.SearchAsync(query, ct), token);
    public Task<LouvorJaState> GetStateAsync(CancellationToken token = default) => ExecuteAsync((client, ct) => client.GetStateAsync(ct), token, publishContent: true);
    public Task OpenAsync(int id, LouvorJaMode mode, CancellationToken token = default) => ExecuteAsync(async (client, ct) => { await client.OpenAsync(id, mode, ct).ConfigureAwait(false); return true; }, token);
    public Task SendAsync(LouvorJaCommand command, int volume = 50, CancellationToken token = default) => ExecuteAsync(async (client, ct) => { await client.SendAsync(command, volume, ct).ConfigureAwait(false); return true; }, token);
    public void Stop()
    {
        CancellationTokenSource epoch;
        lock (_sync) { if (_stopped) return; _stopped = true; epoch = _epoch; }
        _lifetime.Cancel(); epoch.Cancel();
    }
    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_monitor is not null) await _monitor.ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false); _gate.Release();
        foreach (var source in _retired) source.Dispose();
        _epoch.Dispose(); _lifetime.Dispose();
    }
}
