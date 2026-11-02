using System.Net.Http;
using System.Windows;
using Sinalo.Application.LouvorJa;
using Sinalo.App.ViewModels;
using Sinalo.Infrastructure.LouvorJa;

namespace Sinalo.App.Views;

public partial class LouvorJaIntegrationView : System.Windows.Controls.UserControl, IDisposable, IAsyncDisposable
{
    private readonly HttpClient? _http;
    private readonly LouvorJaLocalConnection? _connection;
    private readonly ILouvorJaSession? _session;
    private Task? _disposal;
    private bool _startupRequested;
    private bool _disposed;
    public LouvorJaIntegrationViewModel Model { get; }
    public LouvorJaIntegrationView(bool useSimulator = false, Func<string?>? playbackConflict = null, ILouvorJaConfigurationStore? configurationStore = null,
        ILouvorJaProcessMonitor? processMonitor = null, ILouvorJaSession? session = null)
    {
        InitializeComponent();
        void Diagnostic(string line) => Dispatcher.BeginInvoke(() => Model?.AddDiagnostic(line));
        if (useSimulator)
        {
            var handler = new LouvorJaMockHandler();
            _http = new HttpClient(handler) { BaseAddress = new Uri("http://louvorja-simulator.invalid/"), Timeout = Timeout.InfiniteTimeSpan };
            Model = new(new LouvorJaHttpClient(_http, Diagnostic), scenario => handler.Scenario = scenario, handler.Reset);
        }
        else
        {
            _connection = session is null ? new(Diagnostic) : null;
            _session = session ?? new LouvorJaSession(processMonitor ?? new WindowsLouvorJaProcessMonitor(), port => _connection!.Create(port));
            Model = new(_session, configurationStore ?? new LouvorJaConfigurationStore(new Sinalo.Infrastructure.LocalSinaloPathService()),
                playbackConflict, action => { if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(action); });
            var configurationLoaded = false;
            Loaded += async (_, _) => { if (!_startupRequested && !configurationLoaded) { configurationLoaded = true; await Model.LoadConfigurationAsync(); } };
            Unloaded += (_, _) => _session.ObserveContent = false;
            IsVisibleChanged += (_, _) => _session.ObserveContent = !_disposed && IsVisible;
        }
        DataContext = Model;
    }
    private void ChooseExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (!Model.CanConnect) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Selecione o Louvor JA.exe instalado", Filter = "Louvor JA (Louvor JA.exe)|Louvor JA.exe", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) Model.ExecutablePath = dialog.FileName;
    }
    public Task InitializeSessionAsync() { _startupRequested = true; return Model.InitializeSessionAsync(); }
    public void Dispose() => _ = DisposeAsync();
    public ValueTask DisposeAsync()
    {
        if (_disposal is not null) return new(_disposal);
        _disposed = true; Model.Dispose();
        _disposal = DrainAsync(); return new(_disposal);
    }
    private async Task DrainAsync()
    {
        try { if (_session is not null) await _session.DisposeAsync().ConfigureAwait(false); }
        finally { _http?.Dispose(); _connection?.Dispose(); }
    }
}
