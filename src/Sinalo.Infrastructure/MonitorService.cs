using Sinalo.Application.Monitors;

namespace Sinalo.Infrastructure;

public sealed class MonitorService : IMonitorService
{
    public Task<IReadOnlyList<OutputProfile>> GetOutputsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outputs = System.Windows.Forms.Screen.AllScreens
            .Select((screen, index) => new OutputProfile(
                screen.DeviceName,
                $"Tela {index + 1}{(screen.Primary ? " · Principal" : string.Empty)}",
                index + 1,
                screen.Bounds.X,
                screen.Bounds.Y,
                screen.Bounds.Width,
                screen.Bounds.Height,
                screen.Primary))
            .OrderByDescending(output => output.IsPrimary)
            .ToArray();

        return Task.FromResult<IReadOnlyList<OutputProfile>>(outputs);
    }
}
