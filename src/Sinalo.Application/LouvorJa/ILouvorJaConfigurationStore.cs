namespace Sinalo.Application.LouvorJa;
public sealed record LouvorJaConfiguration(string ExecutablePath, int Port = 7071, bool Enabled = false);
public interface ILouvorJaConfigurationStore
{
    Task<LouvorJaConfiguration?> LoadAsync(CancellationToken token = default);
    // Updates connection fields only, preserving the latest persisted opt-in.
    Task SaveAsync(LouvorJaConfiguration configuration, CancellationToken token = default);
    Task SetEnabledAsync(bool enabled, CancellationToken token = default);
}
