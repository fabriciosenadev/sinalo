using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinalo.Application.LouvorJa;
using Sinalo.Infrastructure.LouvorJa;

namespace Sinalo.App.ViewModels;

public sealed record LouvorJaScenarioOption(LouvorJaScenario Scenario, string Label);
public sealed record LouvorJaModeOption(LouvorJaMode Mode, string Label);

public sealed partial class LouvorJaIntegrationViewModel : ObservableObject, IDisposable
{
    private ILouvorJaClient _client;
    private readonly Func<int, ILouvorJaClient>? _clientFactory;
    private readonly Func<string, CancellationToken, Task>? _launch;
    private readonly Func<string?>? _playbackConflict;
    private readonly ILouvorJaConfigurationStore? _configurationStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private int _revision;
    private readonly Action<LouvorJaScenario> _setScenario;
    private readonly Action _reset;
    private CancellationTokenSource? _active;
    private bool _disposed;
    private readonly ILouvorJaSession? _session;
    private readonly Action<Action> _dispatch = action => action();
    private long _snapshotSequence = -1;
    private bool _configurationLoaded;
    private Task? _initialization;
    private CancellationTokenSource? _searchCancellation;
    private int _searchRevision;
    private bool _searchNeedsRefresh;
    public TimeSpan SearchDelay { get; init; } = TimeSpan.FromMilliseconds(350);
    [ObservableProperty] private bool isSearching;
    [ObservableProperty] private string searchStatus = "Digite para pesquisar após conectar ao Louvor JA.";
    [ObservableProperty] private LouvorJaConnectionState connectionState;
    public LouvorJaIntegrationViewModel(ILouvorJaClient client, Action<LouvorJaScenario> setScenario, Action reset)
    { _client = client; _setScenario = setScenario; _reset = reset; }

    public LouvorJaIntegrationViewModel(Func<int, ILouvorJaClient> clientFactory, Func<string, CancellationToken, Task> launch, Func<string?>? playbackConflict = null, ILouvorJaConfigurationStore? configurationStore = null)
        : this(clientFactory(7071), _ => { }, () => { })
    {
        _clientFactory = clientFactory; _launch = launch; _playbackConflict = playbackConflict;
        _configurationStore = configurationStore;
        Query = "";
        Message = "Abra o Louvor JA, ative seu servidor de transmissão e conecte. Abrir música acionará áudio/projeção reais conforme as configurações dele.";
    }
    public LouvorJaIntegrationViewModel(ILouvorJaSession session, ILouvorJaConfigurationStore configurationStore,
        Func<string?>? playbackConflict = null, Action<Action>? dispatch = null)
        : this(_ => session, (_, _) => Task.CompletedTask, playbackConflict, configurationStore)
    {
        _session = session; _dispatch = dispatch ?? _dispatch;
        session.Changed += SessionChanged;
        ApplySnapshot(session.Current);
    }
    private void SessionChanged(LouvorJaConnectionSnapshot snapshot) => _dispatch(() => ApplySnapshot(snapshot));
    private void ApplySnapshot(LouvorJaConnectionSnapshot snapshot)
    {
        if (_disposed || snapshot.Sequence <= _snapshotSequence) return;
        _snapshotSequence = snapshot.Sequence;
        var changed = ConnectionState != snapshot.Connection;
        ConnectionState = snapshot.Connection;
        IsConnected = snapshot.Connection == LouvorJaConnectionState.Connected;
        State = snapshot.Presentation;
        if (!IsConnected) SelectedSong = null;
        if (changed || !IsConnected) Message = snapshot.Message;
    }
    partial void OnConnectionStateChanged(LouvorJaConnectionState value)
    { OnPropertyChanged(nameof(ConnectionLabel)); NotifyCommands(); }
    public bool IsSimulation => _clientFactory is null;
    public bool IsReal => !IsSimulation;
    public string PageTitle => IsSimulation ? "Louvor JA Flute — integração de teste" : "Louvor JA Flute — controle integrado";
    public string SafetyNotice => IsSimulation ? "MODO SIMULADO · Nenhum comando será enviado ao Louvor JA. Não há áudio, projeção, acesso ao catálogo real ou conexões de rede."
        : "LOUVOR JA FLUTE · Integração exclusiva com o projeto flute-app, não compatível com o Louvor JA legado. Os controles acionam áudio e projeção reais. Configure as telas de saída no Flute.";
    public string ConnectCaption => IsSimulation ? "Conectar ao simulador" : "Tentar conectar novamente";
    [ObservableProperty] private string portText = "7071";
    [ObservableProperty] private string executablePath = LouvorJaApplicationLauncher.DefaultExecutable;
    public string EndpointLabel => $"Conexão local: http://127.0.0.1:{PortText}/";
    partial void OnPortTextChanged(string value) { ConfigurationEdited(); OnPropertyChanged(nameof(EndpointLabel)); }
    partial void OnExecutablePathChanged(string value) => ConfigurationEdited();
    private void ConfigurationEdited() { DisconnectLocalState(); _session?.InvalidateConfiguration(); }
    private void DisconnectLocalState() { _revision++; IsConnected = false; State = null; Songs.Clear(); SelectedSong = null; }

    public IReadOnlyList<LouvorJaScenarioOption> Scenarios { get; } =
    [new(LouvorJaScenario.Normal, "Funcionamento normal"), new(LouvorJaScenario.Unavailable, "Aplicativo indisponível"),
     new(LouvorJaScenario.Slow, "Resposta demorada / timeout"), new(LouvorJaScenario.Empty, "Catálogo vazio"),
     new(LouvorJaScenario.InvalidResponse, "Resposta inválida"), new(LouvorJaScenario.NoEffect, "Comando aceito, sem efeito"),
     new(LouvorJaScenario.Disconnect, "Desconexão ao enviar comando")];
    public IReadOnlyList<LouvorJaModeOption> Modes { get; } =
    [new(LouvorJaMode.Sung, "Cantado"), new(LouvorJaMode.Instrumental, "Instrumental"), new(LouvorJaMode.Silent, "Sem áudio")];
    public ObservableCollection<LouvorJaSong> Songs { get; } = [];
    public ObservableCollection<string> Diagnostics { get; } = [];
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private string query = "teste";
    [ObservableProperty] private string message = "Conecte ao simulador para começar. O Louvor JA instalado não será acessado.";
    [ObservableProperty] private LouvorJaSong? selectedSong;
    [ObservableProperty] private LouvorJaScenarioOption selectedScenario = new(LouvorJaScenario.Normal, "Funcionamento normal");
    [ObservableProperty] private LouvorJaModeOption selectedMode = new(LouvorJaMode.Sung, "Cantado");
    [ObservableProperty] private int requestedVolume = 50;
    [ObservableProperty] private LouvorJaState? state;
    public bool CanChangeScenario => !IsBusy && !_disposed;
    public bool IsWorking => IsBusy || (_session is not null && ConnectionState == LouvorJaConnectionState.Waiting);
    public bool CanDisconnect => !_disposed && (IsReal || !IsWorking);
    public bool CanConnect => !IsWorking && !_disposed;
    public bool CanEditSearch => IsConnected && !IsWorking && !_disposed;
    public bool CanSearch => CanEditSearch && !string.IsNullOrWhiteSpace(Query);
    public bool CanOpen => IsConnected && !IsWorking && !IsSearching && SelectedSong is not null && (SelectedMode.Mode != LouvorJaMode.Instrumental || SelectedSong.HasInstrumental);
    public bool CanControl => IsConnected && !IsWorking && State?.Type == "media";
    public bool CanRefresh => IsConnected && !IsWorking;
    public bool CanNext => CanControl && State!.SlideIndex < State.TotalSlides - 1;
    public bool CanPrevious => CanControl && State!.SlideIndex > 0;
    public string ConnectionLabel => _session is null
        ? IsConnected ? (IsSimulation ? "Conectado ao simulador — sem rede" : "Conectado ao Louvor JA nesta máquina") : "Desconectado"
        : ConnectionState switch
        {
            LouvorJaConnectionState.Closed => "Louvor JA fechado",
            LouvorJaConnectionState.Waiting => "Aguardando o Louvor JA ficar pronto…",
            LouvorJaConnectionState.Unavailable => "Louvor JA aberto · servidor de controle indisponível",
            LouvorJaConnectionState.Connected => "Louvor JA conectado e pronto",
            LouvorJaConnectionState.Lost => "Louvor JA aberto · comunicação perdida",
            LouvorJaConnectionState.ManualDisconnected => "Desconectado pelo operador · reconexão automática suspensa",
            LouvorJaConnectionState.Unknown => "Não foi possível verificar o Louvor JA",
            _ => "Conexão ainda não verificada"
        };
    public string PresentationTitle => State is { Type: not "idle" } ? State.Title : "Nenhuma música aberta";
    public string StateLabel => State?.Type == "media" ? $"{(State.IsPaused ? "Pausado / sem áudio" : IsSimulation ? "Reproduzindo (simulação)" : "Reproduzindo no Louvor JA")} · slide {State.SlideIndex + 1}/{State.TotalSlides} · volume {State.Volume}%"
        : State?.Type == "bible" ? "Bíblia apresentada no Louvor JA — somente consulta nesta integração" : "Sem apresentação musical";

    partial void OnIsBusyChanged(bool value) { NotifyCommands(); if (!value) QueuePendingSearch(); }
    partial void OnIsConnectedChanged(bool value)
    {
        CancelSearch(); _searchNeedsRefresh = !string.IsNullOrWhiteSpace(Query);
        SearchStatus = value ? "Digite para pesquisar músicas automaticamente." : "Conecte ao Louvor JA para pesquisar músicas.";
        OnPropertyChanged(nameof(ConnectionLabel)); NotifyCommands();
        if (value) QueuePendingSearch();
    }
    partial void OnSelectedSongChanged(LouvorJaSong? value) => NotifyCommands();
    partial void OnSelectedModeChanged(LouvorJaModeOption value) => NotifyCommands();
    partial void OnQueryChanged(string value)
    {
        CancelSearch(); Songs.Clear(); SelectedSong = null;
        _searchNeedsRefresh = !string.IsNullOrWhiteSpace(value);
        SearchStatus = _searchNeedsRefresh ? "Digite para pesquisar músicas automaticamente." : "Digite o nome, número do hino ou trecho da letra.";
        NotifyCommands(); QueuePendingSearch();
    }
    partial void OnIsSearchingChanged(bool value) => NotifyCommands();
    private void CancelSearch()
    {
        _searchRevision++;
        var previous = _searchCancellation; _searchCancellation = null;
        previous?.Cancel(); IsSearching = false;
    }
    private void QueuePendingSearch()
    {
        if (_searchNeedsRefresh && CanSearch && !IsSearching) _ = StartSearchAsync(immediate: false);
    }
    private Task StartSearchAsync(bool immediate)
    {
        CancelSearch();
        if (!CanSearch) return Task.CompletedTask;
        _searchNeedsRefresh = false;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _searchCancellation = cancellation;
        var revision = _searchRevision;
        var query = Query.Trim();
        Songs.Clear(); SelectedSong = null; IsSearching = true;
        SearchStatus = immediate ? "Pesquisando músicas…" : "Aguardando você terminar de digitar…";
        return SearchCoreAsync(query, revision, cancellation, immediate);
    }
    private async Task SearchCoreAsync(string query, int revision, CancellationTokenSource cancellation, bool immediate)
    {
        var entered = false;
        bool Current() => !_disposed && !cancellation.IsCancellationRequested && revision == _searchRevision && IsConnected;
        try
        {
            if (!immediate) await Task.Delay(SearchDelay, cancellation.Token);
            await _gate.WaitAsync(cancellation.Token); entered = true;
            if (!Current() || IsBusy) return;
            SearchStatus = "Pesquisando músicas…";
            var songs = await _client.SearchAsync(query, cancellation.Token);
            if (!Current()) return;
            foreach (var song in songs) Songs.Add(song);
            SearchStatus = songs.Count == 0 ? "Nenhuma música encontrada. Tente outro nome, número ou trecho."
                : $"{songs.Count} música(s) encontrada(s). Selecione uma para abrir.";
            Message = SearchStatus;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!Current()) return;
            IsConnected = false; State = null;
            SearchStatus = $"Não foi possível pesquisar: {error.Message}"; Message = SearchStatus; AddDiagnostic(Message);
        }
        finally
        {
            if (entered) _gate.Release();
            if (revision == _searchRevision)
            { _searchCancellation = null; IsSearching = false; }
            cancellation.Dispose();
        }
    }
    partial void OnStateChanged(LouvorJaState? value)
    { OnPropertyChanged(nameof(PresentationTitle)); OnPropertyChanged(nameof(StateLabel)); NotifyCommands(); }
    partial void OnSelectedScenarioChanged(LouvorJaScenarioOption value)
    {
        _setScenario(value.Scenario); _reset(); IsConnected = false; State = null; Songs.Clear(); SelectedSong = null;
        Message = "Cenário alterado. Conecte novamente ao simulador.";
        AddDiagnostic($"Cenário: {value.Label}");
    }
    private void NotifyCommands()
    {
        foreach (var name in new[] { nameof(IsWorking), nameof(CanEditSearch), nameof(CanDisconnect), nameof(CanChangeScenario), nameof(CanConnect), nameof(CanSearch), nameof(CanOpen), nameof(CanControl), nameof(CanRefresh), nameof(CanNext), nameof(CanPrevious) }) OnPropertyChanged(name);
        ConnectCommand.NotifyCanExecuteChanged(); SearchCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged();
        ToggleCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged(); ApplyVolumeCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        LaunchCommand.NotifyCanExecuteChanged(); DisconnectCommand.NotifyCanExecuteChanged();
        SaveConfigurationCommand.NotifyCanExecuteChanged(); RestoreDefaultPathCommand.NotifyCanExecuteChanged();
    }
    public void AddDiagnostic(string line)
    {
        if (_disposed) return;
        Diagnostics.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");
        while (Diagnostics.Count > 100) Diagnostics.RemoveAt(Diagnostics.Count - 1);
    }
    private async Task RunAsync(string operation, Func<CancellationToken, Task> action)
    {
        if (IsBusy || _disposed) return;
        if (IsSearching) _searchNeedsRefresh = true;
        CancelSearch();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _active = cancellation; IsBusy = true; Message = operation;
        var entered = false;
        try { await _gate.WaitAsync(cancellation.Token); entered = true; await action(cancellation.Token); }
        catch (OperationCanceledException)
        {
            State = null; IsConnected = false;
            Message = _session?.Current.Connection == LouvorJaConnectionState.ManualDisconnected ? _session.Current.Message
                : IsSimulation ? "Operação cancelada. Nenhum comando real foi enviado." : "Espera cancelada. Um comando já recebido pelo Louvor JA pode ter sido executado; confira o aplicativo antes de repetir.";
        }
        catch (Exception error)
        {
            State = null; IsConnected = false;
            Message = $"Não foi possível concluir: {error.Message}" + (IsReal
                ? " Confira se o Louvor JA está aberto, com o servidor ativo na porta informada. Um comando enviado pode ter sido executado; confira antes de repetir."
                : "");
            AddDiagnostic(Message);
        }
        finally { if (entered) _gate.Release(); _active = null; IsBusy = false; }
    }
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => RunAsync("Conectando…", async token =>
    {
        if (_clientFactory is not null)
        {
            DisconnectLocalState();
            if (!int.TryParse(PortText, out var port) || port is < 1 or > 65535) throw new InvalidOperationException("Informe uma porta entre 1 e 65535.");
            _client = _clientFactory(port);
        }
        if (_session is not null)
        {
            _session.Configure(ReadConfiguration()); await _session.ConnectAsync(token);
            ApplySnapshot(_session.Current); return;
        }
        await _client.ConnectAsync(token); State = await _client.GetStateAsync(token); IsConnected = true;
        Message = IsSimulation ? "Simulador conectado. Pesquise por “teste” para listar as músicas fictícias." : "Conexão confirmada. Pesquise uma música do catálogo do Louvor JA.";
    });
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private Task SearchAsync() => StartSearchAsync(immediate: true);
    [RelayCommand(CanExecute = nameof(CanOpen))]
    private Task OpenAsync()
    {
        if (!CanOpen) return Task.CompletedTask;
        if (_playbackConflict?.Invoke() is { } conflict) { Message = conflict; return Task.CompletedTask; }
        var song = SelectedSong!; var mode = SelectedMode.Mode;
        return RunAsync("Solicitando abertura no Louvor JA…", async token =>
        {
            await _client.OpenAsync(song.Id, mode, token);
            bool Opened(LouvorJaState state) => state.Type == "media" && state.Title == song.Name && (mode == LouvorJaMode.Silent || !state.IsPaused);
            State = await ObserveUntilAsync(Opened, token);
            Message = Opened(State)
                ? IsSimulation ? "Estado simulado de abertura observado. Nenhum áudio/projeção real foi iniciado." : "Música observada no Louvor JA. A projeção segue as configurações dele."
                : "Comando recebido, mas a abertura não foi confirmada pelo estado.";
        });
    }
    private Task ControlAsync(LouvorJaCommand command)
    {
        if (!CanControl) return Task.CompletedTask;
        if (command == LouvorJaCommand.TogglePlayback && State!.IsPaused && _playbackConflict?.Invoke() is { } conflict) { Message = conflict; return Task.CompletedTask; }
        var before = State!; var volume = RequestedVolume;
        return RunAsync("Enviando comando e conferindo estado…", async token =>
        {
            bool Matches(LouvorJaState observed) => command switch
            {
                LouvorJaCommand.TogglePlayback => observed.IsPaused != before.IsPaused,
                LouvorJaCommand.NextSlide => observed.SlideIndex == before.SlideIndex + 1,
                LouvorJaCommand.PreviousSlide => observed.SlideIndex == before.SlideIndex - 1,
                LouvorJaCommand.Close => observed.Type == "idle",
                LouvorJaCommand.Volume => observed.Volume == volume,
                _ => false
            };
            await _client.SendAsync(command, volume, token); State = await ObserveUntilAsync(Matches, token);
            Message = Matches(State) ? IsSimulation ? "Estado simulado atualizado e observado." : "Mudança de estado observada no Louvor JA." : "Comando recebido, mas a mudança não foi confirmada pelo estado. Confira o Louvor JA antes de repetir.";
        });
    }
    [RelayCommand(CanExecute = nameof(CanControl))] private Task ToggleAsync() => ControlAsync(LouvorJaCommand.TogglePlayback);
    [RelayCommand(CanExecute = nameof(CanNext))] private Task NextAsync() => ControlAsync(LouvorJaCommand.NextSlide);
    [RelayCommand(CanExecute = nameof(CanPrevious))] private Task PreviousAsync() => ControlAsync(LouvorJaCommand.PreviousSlide);
    [RelayCommand(CanExecute = nameof(CanControl))] private Task CloseAsync() => ControlAsync(LouvorJaCommand.Close);
    [RelayCommand(CanExecute = nameof(CanControl))] private Task ApplyVolumeAsync() => ControlAsync(LouvorJaCommand.Volume);
    [RelayCommand(CanExecute = nameof(CanRefresh))] private Task RefreshAsync() => RunAsync("Consultando estado…", async token => { State = await _client.GetStateAsync(token); Message = "Estado consultado."; });
    private async Task<LouvorJaState> ObserveUntilAsync(Func<LouvorJaState, bool> matches, CancellationToken token)
    {
        var observed = await _client.GetStateAsync(token);
        for (var attempt = 0; IsReal && !matches(observed) && attempt < 6; attempt++)
        { await Task.Delay(300, token); observed = await _client.GetStateAsync(token); }
        return observed;
    }
    public async Task ObserveAsync()
    {
        if (_disposed || !IsConnected || IsBusy || !await _gate.WaitAsync(0)) return;
        var revision = _revision;
        try
        {
            var current = await _client.GetStateAsync(_lifetime.Token);
            if (revision == _revision && !_disposed) State = current;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { if (revision == _revision && !_disposed) { State = null; IsConnected = false; Message = $"Conexão perdida: {error.Message} Reconecte para continuar."; } }
        finally { _gate.Release(); }
    }
    [RelayCommand(CanExecute = nameof(CanConnect))] private Task LaunchAsync() => RunAsync("Abrindo Louvor JA…", async token =>
    {
        if (_session is not null)
        {
            _session.Configure(ReadConfiguration()); await _session.OpenAndConnectAsync(token);
            ApplySnapshot(_session.Current); return;
        }
        if (_launch is null) return;
        await _launch(ExecutablePath, token);
        Message = "Abertura solicitada. Aguarde o Louvor JA ficar pronto, ative o servidor e clique em Conectar.";
    });
    [RelayCommand(CanExecute = nameof(CanDisconnect))] private void Disconnect()
    {
        if (_session is not null) { _session.Disconnect(); _active?.Cancel(); ApplySnapshot(_session.Current); return; }
        DisconnectLocalState(); Message = "Desconectado. O Louvor JA continua aberto e sua reprodução não foi interrompida.";
    }
    [RelayCommand(CanExecute = nameof(IsWorking))] private void Cancel() { _session?.Disconnect(); _active?.Cancel(); }
    private LouvorJaConfiguration ReadConfiguration()
    {
        if (!int.TryParse(PortText, out var port) || port is < 1 or > 65535) throw new ArgumentException("Informe uma porta entre 1 e 65535.");
        return new(ExecutablePath, port);
    }
    public Task InitializeSessionAsync() => _initialization ??= InitializeCoreAsync();
    private async Task InitializeCoreAsync()
    {
        if (!_configurationLoaded) await LoadConfigurationAsync();
        if (_disposed || !_configurationLoaded || _session is null) return;
        await RunAsync("Verificando se o Louvor JA já está aberto…", async token =>
        {
            await _session.StartAsync(ReadConfiguration(), token); ApplySnapshot(_session.Current);
        });
    }
    public Task LoadConfigurationAsync() => RunAsync("Carregando configuração da integração…", async token =>
    {
        var saved = _configurationStore is null ? null : await _configurationStore.LoadAsync(token);
        if (saved is not null) { ExecutablePath = saved.ExecutablePath; PortText = saved.Port.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        _configurationLoaded = true;
        Message = "Use Abrir Louvor JA para iniciar e conectar automaticamente. O servidor de transmissão precisa estar ativo no Louvor JA.";
    });
    [RelayCommand(CanExecute = nameof(CanConnect))] private Task SaveConfigurationAsync() => RunAsync("Salvando configuração…", async token =>
    {
        if (_configurationStore is null) { Message = "O simulador não grava configurações."; return; }
        if (!int.TryParse(PortText, out var port)) throw new ArgumentException("Informe uma porta numérica entre 1 e 65535.");
        await _configurationStore.SaveAsync(new(ExecutablePath, port), token);
        ExecutablePath = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(ExecutablePath.Trim()));
        Message = "Caminho e porta salvos neste perfil do Sinalo. Nenhuma configuração do Louvor JA foi alterada.";
    });
    [RelayCommand(CanExecute = nameof(CanConnect))] private void RestoreDefaultPath()
    { ExecutablePath = LouvorJaApplicationLauncher.DefaultExecutable; Message = "Caminho padrão restaurado. Use Salvar caminho e porta para guardar a alteração."; }
    [RelayCommand(CanExecute = nameof(CanChangeScenario))] private void Reset()
    {
        _reset(); State = null; IsConnected = false; Songs.Clear(); SelectedSong = null; Diagnostics.Clear();
        Message = "Simulação reiniciada. Conecte para começar.";
    }
    public void Dispose()
    {
        _disposed = true; _revision++; CancelSearch(); _lifetime.Cancel(); _active?.Cancel();
        if (_session is not null) { _session.Changed -= SessionChanged; _session.Stop(); }
    }
}
