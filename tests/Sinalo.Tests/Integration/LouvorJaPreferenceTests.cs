using System.IO;
using System.Text.Json;
using Sinalo.Application.LouvorJa;
using Sinalo.Infrastructure;
using Sinalo.Infrastructure.LouvorJa;

namespace Sinalo.Tests.Integration;

public sealed class LouvorJaPreferenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Sinalo-Flute-preference-" + Guid.NewGuid().ToString("N"));
    private readonly LocalSinaloPathService _paths;
    private readonly LouvorJaConfigurationStore _store;
    private string FilePath => Path.Combine(_paths.GetPaths().DataPath, "louvor-ja-integration.json");
    public LouvorJaPreferenceTests()
    {
        _paths = new(rootPath: _root); Directory.CreateDirectory(_paths.GetPaths().DataPath);
        _store = new(_paths);
    }
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task DefaultIsDisabled_AndCanEnableBeforeInstallingFlute()
    {
        Assert.Null(await _store.LoadAsync());
        await _store.SetEnabledAsync(true);
        var saved = (await _store.LoadAsync())!;
        Assert.True(saved.Enabled); Assert.Equal(7071, saved.Port);
        Assert.Equal(LouvorJaApplicationLauncher.DefaultExecutable, saved.ExecutablePath);
        await _store.SetEnabledAsync(false);
        Assert.Equal(saved with { Enabled = false }, await _store.LoadAsync());
    }

    [Fact]
    public async Task LegacyJson_DefaultsToDisabled_AndPreservesMissingExecutableAndPort()
    {
        var executable = Path.Combine(_root, "Uninstalled", "Louvor JA.exe");
        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(new { ExecutablePath = executable, Port = 7878 }));
        var legacy = (await _store.LoadAsync())!;
        Assert.False(legacy.Enabled);
        await _store.SetEnabledAsync(true); Assert.Equal(legacy with { Enabled = true }, await _store.LoadAsync());
        await _store.SetEnabledAsync(false); Assert.Equal(legacy, await _store.LoadAsync());
    }

    [Fact]
    public async Task ConnectionSave_CannotResetLatestOptIn_EvenWithStaleSnapshot()
    {
        var executable = Path.Combine(_root, "Louvor JA.exe"); await File.WriteAllTextAsync(executable, "Fixture; never launched.");
        await _store.SetEnabledAsync(true);
        await _store.SaveAsync(new(executable, 7777));
        Assert.Equal(new(executable, 7777, true), await _store.LoadAsync());
        await _store.SetEnabledAsync(false);
        await _store.SaveAsync(new(executable, 8888, true));
        Assert.Equal(new(executable, 8888, false), await _store.LoadAsync());
        await Task.WhenAll(_store.SetEnabledAsync(true), _store.SaveAsync(new(executable, 9999)));
        Assert.Equal(new(executable, 9999, true), await _store.LoadAsync());
        Assert.Empty(Directory.GetFiles(_paths.GetPaths().DataPath, "*.tmp"));
    }

    [Fact]
    public async Task CorruptOrLockedConfig_IsNeverReplacedByDefaultPreference()
    {
        const string corrupted = "{invalid";
        await File.WriteAllTextAsync(FilePath, corrupted);
        await Assert.ThrowsAsync<JsonException>(() => _store.SetEnabledAsync(true));
        Assert.Equal(corrupted, await File.ReadAllTextAsync(FilePath));
        File.Delete(FilePath); await _store.SetEnabledAsync(true);
        var previous = await File.ReadAllTextAsync(FilePath);
        using (var exclusive = File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => _store.SetEnabledAsync(false));
        Assert.Equal(previous, await File.ReadAllTextAsync(FilePath));
        Assert.Empty(Directory.GetFiles(_paths.GetPaths().DataPath, "*.tmp"));
    }

    [Fact]
    public async Task CancelledUpdate_DoesNotOverwriteConfiguration()
    {
        await _store.SetEnabledAsync(true);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SetEnabledAsync(false, cancellation.Token));
        Assert.True((await _store.LoadAsync())!.Enabled);
    }
}
