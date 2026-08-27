using Sinalo.Application.Monitors;
using Sinalo.Application.Playback;
using Sinalo.Application.Presentation;

namespace Sinalo.App;

public sealed class PresentationOutputService(IMonitorService monitorService, IPresentationWindowFactory windowFactory) : IPresentationOutputService
{
    private readonly IMonitorService _monitorService = monitorService;
    private readonly IPresentationWindowFactory _windowFactory = windowFactory;
    private IPresentationWindowHost? _window;

    public bool IsOpen => _window?.IsVisible == true;

    public async Task<PresentationOutputResult> ShowAsync(PresentationScene scene, OutputProfile requestedOutput, CancellationToken cancellationToken = default)
    {
        var availableOutputs = await _monitorService.GetOutputsAsync(cancellationToken);
        var output = OutputSelectionResolver.Resolve(new PlaybackConfiguration(requestedOutput.ScreenNumber, requestedOutput.MonitorKey), availableOutputs);
        if (output is null) return new(false, "A tela de saída não está disponível. Verifique os monitores conectados ao Windows.");

        if (_window is null || !_window.IsVisible) _window = _windowFactory.Create();
        _window.Display(scene, output);
        return new(true, $"Apresentação aberta em {output.DisplayName}.");
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_window?.IsVisible == true) _window.Close();
        _window = null;
        return Task.CompletedTask;
    }
}
