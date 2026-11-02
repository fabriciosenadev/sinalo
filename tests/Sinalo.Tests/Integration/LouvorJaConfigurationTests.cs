using System.IO;
using System.Net.Http;
using System.Text.Json;
using Sinalo.Application.LouvorJa;
using Sinalo.App.ViewModels;
using Sinalo.Infrastructure;
using Sinalo.Infrastructure.LouvorJa;
namespace Sinalo.Tests.Integration;

public sealed class LouvorJaConfigurationTests
{
    [Fact]
    public async Task Configuration_RoundTripsSeparatelyWithoutLaunchingOrOverwritingOnInvalidInput()
    {
        var root = Path.Combine(Path.GetTempPath(), "Sinalo-LouvorJA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new LocalSinaloPathService(rootPath: root);
            var store = new LouvorJaConfigurationStore(paths);
            Assert.Null(await store.LoadAsync());
            var executable = Path.Combine(root, "Louvor JA.exe");
            await File.WriteAllTextAsync(executable, "Test fixture, never executed.");
            var saved = new LouvorJaConfiguration(executable, 7777);
            await store.SaveAsync(saved); Assert.Equal(saved, await store.LoadAsync());
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(saved with { Port = 0 }));
            await Assert.ThrowsAsync<FileNotFoundException>(() => store.SaveAsync(saved with { ExecutablePath = Path.Combine(root, "outro.exe") }));
            Assert.Equal(saved, await store.LoadAsync());
            Assert.Empty(Directory.GetFiles(paths.GetPaths().DataPath, "*.tmp"));
            using var http = new HttpClient(new LouvorJaMockHandler()) { BaseAddress = new Uri("http://test.invalid/") };
            var client = new LouvorJaHttpClient(http);
            using var vm = new LouvorJaIntegrationViewModel(_ => client, (_, _) => throw new InvalidOperationException("Must never launch"), configurationStore: store);
            await vm.LoadConfigurationAsync(); Assert.Equal(executable, vm.ExecutablePath); Assert.Equal("7777", vm.PortText); Assert.False(vm.IsConnected);
            vm.PortText = "8888"; await vm.SaveConfigurationCommand.ExecuteAsync(null);
            Assert.Equal(8888, (await store.LoadAsync())!.Port);
            vm.RestoreDefaultPathCommand.Execute(null); Assert.Equal(LouvorJaApplicationLauncher.DefaultExecutable, vm.ExecutablePath);
            Assert.Equal(executable, (await store.LoadAsync())!.ExecutablePath); // restore requires explicit save
            vm.PortText = "inválido"; await vm.SaveConfigurationCommand.ExecuteAsync(null); Assert.Contains("numérica", vm.Message);
            var file = Path.Combine(paths.GetPaths().DataPath, "louvor-ja-integration.json");
            await File.WriteAllTextAsync(file, "{corrompido"); await Assert.ThrowsAsync<JsonException>(() => store.LoadAsync());
            await File.WriteAllTextAsync(file, "null"); await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(saved with { Port = -1 })); await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DefaultExecutable_BelongsToCurrentUserNotDeveloper()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Louvor JA", "Louvor JA.exe"), LouvorJaApplicationLauncher.DefaultExecutable);
    }
}
