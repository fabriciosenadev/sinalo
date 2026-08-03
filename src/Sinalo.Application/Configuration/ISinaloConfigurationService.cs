namespace Sinalo.Application.Configuration;

public interface ISinaloConfigurationService
{
    Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken cancellationToken = default);
    Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources, CancellationToken cancellationToken = default);
}
