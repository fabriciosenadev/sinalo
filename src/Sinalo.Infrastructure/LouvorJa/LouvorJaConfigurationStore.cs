using System.Text.Json;
using Sinalo.Application.LouvorJa;
using Sinalo.Application.Storage;
namespace Sinalo.Infrastructure.LouvorJa;

public sealed class LouvorJaConfigurationStore(ISinaloPathService paths) : ILouvorJaConfigurationStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string ConfigurationPath => Path.Combine(paths.GetPaths().DataPath, "louvor-ja-integration.json");
    public async Task<LouvorJaConfiguration?> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { return await ReadAsync(token).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task<LouvorJaConfiguration?> ReadAsync(CancellationToken token)
    {
        FileStream stream;
        try { stream = File.OpenRead(ConfigurationPath); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        using var ownedStream = stream;
        var configuration = await JsonSerializer.DeserializeAsync<LouvorJaConfiguration>(stream, cancellationToken: token)
            ?? throw new InvalidDataException("Configuração da integração inválida. Confira o caminho e a porta e salve novamente.");
        if (configuration.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(configuration.ExecutablePath))
            throw new InvalidDataException("Configuração da integração inválida. Confira o caminho e a porta e salve novamente.");
        return configuration;
    }
    public async Task SetEnabledAsync(bool enabled, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var previous = await ReadAsync(token).ConfigureAwait(false) ?? new(LouvorJaApplicationLauncher.DefaultExecutable);
            await WriteAsync(previous with { Enabled = enabled }, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async Task SaveAsync(LouvorJaConfiguration configuration, CancellationToken token = default)
    {
        if (configuration.Port is < 1 or > 65535) throw new ArgumentException("Informe uma porta entre 1 e 65535.");
        var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuration.ExecutablePath.Trim()));
        if (executable.StartsWith(@"\\", StringComparison.Ordinal) || !Path.GetFileName(executable).Equals("Louvor JA.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
            throw new FileNotFoundException("Selecione o arquivo Louvor JA.exe instalado, não apenas a pasta.");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var previous = await ReadAsync(token).ConfigureAwait(false);
            await WriteAsync(configuration with { ExecutablePath = executable, Enabled = previous?.Enabled ?? false }, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    private async Task WriteAsync(LouvorJaConfiguration configuration, CancellationToken token)
    {
        Directory.CreateDirectory(paths.GetPaths().DataPath);
        var staging = ConfigurationPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(staging, JsonSerializer.Serialize(configuration), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(staging, ConfigurationPath, overwrite: true);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}
