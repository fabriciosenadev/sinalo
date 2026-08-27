namespace Sinalo.Application.Monitors;

public interface IMonitorService
{
    Task<IReadOnlyList<OutputProfile>> GetOutputsAsync(CancellationToken cancellationToken = default);
}
