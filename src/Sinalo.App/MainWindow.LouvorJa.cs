using System.Windows;
using Sinalo.Application.LouvorJa;
using Sinalo.App.ViewModels;
using Sinalo.App.Views;

namespace Sinalo.App;

public partial class MainWindow
{
    public ILouvorJaConfigurationStore? LouvorJaConfigurationStore { get; init; }
    public Func<LouvorJaIntegrationView>? LouvorJaViewFactory { get; init; }
    private readonly SemaphoreSlim _louvorJaTransition = new(1, 1);
    private readonly CancellationTokenSource _louvorJaWindowLifetime = new();
    private LouvorJaIntegrationView? _louvorJaView;
    private int _louvorJaGeneration;
    private bool _louvorJaClosing;

    private void InitializeLouvorJaIntegration()
    {
        Loaded += async (_, _) => await RefreshLouvorJaIntegrationAsync();
        LouvorJaNavigationButton.Click += async (_, _) =>
        {
            if (_louvorJaView is null || _louvorJaClosing || !await CanLeaveLibraryAsync()) return;
            if (_louvorJaView is null || _louvorJaClosing) return;
            LouvorJaWorkspaceHost.Content = _louvorJaView;
            (DataContext as HomeViewModel)?.SelectLouvorJaWorkspace();
        };
        Closed += async (_, _) =>
        {
            _louvorJaClosing = true; _louvorJaGeneration++;
            _louvorJaWindowLifetime.Cancel();
            // Stop synchronously before any await: shutdown must not wait for readiness.
            var pending = _louvorJaView?.DisposeAsync();
            _louvorJaView = null;
            if (pending is { } disposal) await disposal.ConfigureAwait(false);
        };
    }

    public async Task RefreshLouvorJaIntegrationAsync()
    {
        if (_louvorJaClosing) return;
        var generation = ++_louvorJaGeneration;
        LouvorJaConfiguration? configuration = null;
        string? error = null;
        try
        {
            if (LouvorJaConfigurationStore is not null)
                configuration = await LouvorJaConfigurationStore.LoadAsync(_louvorJaWindowLifetime.Token);
        }
        catch (OperationCanceledException) when (_louvorJaClosing) { return; }
        catch (Exception)
        { error = "Integração Louvor JA Flute desativada: não foi possível ler a configuração. Confira a aba Integrações nas Configurações e tente novamente."; }
        if (_louvorJaClosing || generation != _louvorJaGeneration) return;
        await _louvorJaTransition.WaitAsync();
        try
        {
            if (_louvorJaClosing || generation != _louvorJaGeneration) return;
            if (configuration?.Enabled != true)
            {
                IntegrationNavigation.Visibility = Visibility.Collapsed;
                LouvorJaWorkspaceHost.Content = null;
                if (DataContext is HomeViewModel { IsLouvorJaWorkspace: true } home) home.SelectGeneralLibraryWorkspace();
                var previous = _louvorJaView; _louvorJaView = null;
                if (previous is not null) await previous.DisposeAsync();
                if (error is not null && DataContext is HomeViewModel current) current.OperationMessage = error;
                return;
            }
            if (_louvorJaView is not null) return; // Saving unrelated settings preserves manual disconnect.
            var view = LouvorJaViewFactory?.Invoke() ?? new LouvorJaIntegrationView(
                configurationStore: LouvorJaConfigurationStore,
                playbackConflict: () => Playback?.Snapshot.IsActive == true
                    || (DataContext as HomeViewModel)?.IsPresentationOpen == true
                    || (DataContext as HomeViewModel)?.WorshipTimer.IsAudioPlaying == true
                    ? "Encerre o vídeo, a apresentação ou o áudio do cronômetro no Sinalo antes de iniciar/retomar a música no Louvor JA Flute." : null);
            _louvorJaView = view;
            IntegrationNavigation.Visibility = Visibility.Visible;
            // Observe errors without holding the transition gate during the 30-second wait.
            _ = StartLouvorJaAsync(view, generation);
        }
        finally { _louvorJaTransition.Release(); }
    }

    private async Task StartLouvorJaAsync(LouvorJaIntegrationView view, int generation)
    {
        try { await view.InitializeSessionAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!_louvorJaClosing && ReferenceEquals(view, _louvorJaView) && generation == _louvorJaGeneration)
                view.Model.Message = "Não foi possível iniciar o controle do Louvor JA Flute. Confira caminho, porta e servidor de transmissão.";
        }
    }
}
