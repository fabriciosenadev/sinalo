using System.Net;
using System.Net.Http;
using Sinalo.Application.LouvorJa;
using Sinalo.Infrastructure.LouvorJa;
using Sinalo.App.ViewModels;

namespace Sinalo.Tests.Integration;

public sealed class LouvorJaMockTests
{
    private static HttpClient Http(LouvorJaMockHandler handler) => new(handler) { BaseAddress = new("http://louvorja-simulator.invalid/"), Timeout = Timeout.InfiniteTimeSpan };
    [Theory]
    [InlineData(LouvorJaMode.Sung, false)]
    [InlineData(LouvorJaMode.Instrumental, false)]
    [InlineData(LouvorJaMode.Silent, true)]
    public async Task FullProtocol_UsesSerializedResponsesWithoutNetwork(LouvorJaMode mode, bool paused)
    {
        var handler = new LouvorJaMockHandler { Latency = TimeSpan.Zero };
        using var http = Http(handler);
        var log = new List<string>();
        var client = new LouvorJaHttpClient(http, log.Add);
        await client.ConnectAsync();
        Assert.Equal("idle", (await client.GetStateAsync()).Type);
        var songs = await client.SearchAsync("teste");
        Assert.Equal(3, songs.Count);
        Assert.Empty(await client.SearchAsync("inexistente"));
        Assert.Empty(await client.SearchAsync(""));
        await client.OpenAsync(songs[0].Id, mode);
        Assert.Equal(paused, (await client.GetStateAsync()).IsPaused);
        await client.SendAsync(LouvorJaCommand.TogglePlayback);
        Assert.Equal(!paused, (await client.GetStateAsync()).IsPaused);
        await client.SendAsync(LouvorJaCommand.NextSlide);
        Assert.Equal(1, (await client.GetStateAsync()).SlideIndex);
        await client.SendAsync(LouvorJaCommand.PreviousSlide);
        Assert.Equal(0, (await client.GetStateAsync()).SlideIndex);
        await client.SendAsync(LouvorJaCommand.Volume, 73);
        Assert.Equal(73, (await client.GetStateAsync()).Volume);
        await client.SendAsync(LouvorJaCommand.Close);
        Assert.Equal("idle", (await client.GetStateAsync()).Type);
        Assert.Contains(log, x => x.Contains($"tag={(int)mode}"));
        using var missing = await http.GetAsync("api/not-supported");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.OpenAsync(0, mode));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.OpenAsync(1, (LouvorJaMode)99));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendAsync(LouvorJaCommand.Volume, 101));
    }

    [Theory]
    [InlineData(LouvorJaScenario.Unavailable)]
    [InlineData(LouvorJaScenario.InvalidResponse)]
    [InlineData(LouvorJaScenario.Slow)]
    public async Task Failure_IsVisibleAndNeverLeavesControlsConnected(LouvorJaScenario scenario)
    {
        var handler = new LouvorJaMockHandler { Latency = TimeSpan.Zero };
        using var http = Http(handler);
        using var vm = new LouvorJaIntegrationViewModel(new LouvorJaHttpClient(http) { RequestTimeout = TimeSpan.FromMilliseconds(50) }, x => handler.Scenario = x, handler.Reset);
        vm.SelectedScenario = vm.Scenarios.Single(x => x.Scenario == scenario);
        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.False(vm.IsConnected); Assert.False(vm.IsBusy); Assert.False(vm.CanControl);
        Assert.Contains("Não foi possível", vm.Message);
    }

    [Fact]
    public async Task OperatorWorkflow_HandlesSelectionCommandsResetAndNoEffect()
    {
        var handler = new LouvorJaMockHandler { Latency = TimeSpan.Zero };
        using var http = Http(handler);
        using var vm = new LouvorJaIntegrationViewModel(new LouvorJaHttpClient(http), x => handler.Scenario = x, handler.Reset);
        Assert.False(vm.CanOpen);
        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Songs.Count);
        vm.SelectedSong = vm.Songs[1]; vm.SelectedMode = vm.Modes[1]; Assert.False(vm.CanOpen);
        vm.SelectedSong = vm.Songs[0]; Assert.True(vm.CanOpen);
        await vm.OpenCommand.ExecuteAsync(null);
        Assert.True(vm.CanControl); Assert.False(vm.CanPrevious);
        await vm.ToggleCommand.ExecuteAsync(null);
        Assert.True(vm.State!.IsPaused);
        await vm.NextCommand.ExecuteAsync(null); await vm.NextCommand.ExecuteAsync(null);
        Assert.False(vm.CanNext); Assert.True(vm.CanPrevious);
        await vm.PreviousCommand.ExecuteAsync(null);
        vm.RequestedVolume = 72; await vm.ApplyVolumeCommand.ExecuteAsync(null);
        Assert.Equal(72, vm.State!.Volume);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.CloseCommand.ExecuteAsync(null); Assert.False(vm.CanControl);
        vm.SelectedScenario = vm.Scenarios.Single(x => x.Scenario == LouvorJaScenario.NoEffect);
        await vm.ConnectCommand.ExecuteAsync(null); await vm.SearchCommand.ExecuteAsync(null);
        vm.SelectedSong = vm.Songs[0]; await vm.OpenCommand.ExecuteAsync(null);
        Assert.Contains("não foi confirmada", vm.Message); Assert.False(vm.CanControl);
        for (var i = 0; i < 120; i++) vm.AddDiagnostic("teste");
        Assert.Equal(100, vm.Diagnostics.Count);
        vm.ResetCommand.Execute(null);
        Assert.False(vm.IsConnected); Assert.Empty(vm.Diagnostics); Assert.Empty(vm.Songs);
    }

    [Theory]
    [InlineData(LouvorJaScenario.Empty)]
    [InlineData(LouvorJaScenario.Disconnect)]
    public async Task EmptyAndDisconnect_HaveExplicitOutcomes(LouvorJaScenario scenario)
    {
        var handler = new LouvorJaMockHandler { Latency = TimeSpan.Zero, Scenario = scenario };
        using var http = Http(handler);
        using var vm = new LouvorJaIntegrationViewModel(new LouvorJaHttpClient(http), x => handler.Scenario = x, handler.Reset);
        await vm.ConnectCommand.ExecuteAsync(null); await vm.SearchCommand.ExecuteAsync(null);
        if (scenario == LouvorJaScenario.Empty) { Assert.Empty(vm.Songs); Assert.Contains("Nenhuma música", vm.Message); }
        else { vm.SelectedSong = vm.Songs[0]; await vm.OpenCommand.ExecuteAsync(null); Assert.False(vm.IsConnected); Assert.Null(vm.State); }
    }

    [Fact]
    public async Task CancellationAndReentrancy_KeepOnlyOneRequestActive()
    {
        var handler = new LouvorJaMockHandler { Scenario = LouvorJaScenario.Slow };
        using var http = Http(handler);
        using var vm = new LouvorJaIntegrationViewModel(new LouvorJaHttpClient(http), _ => { }, handler.Reset);
        var first = vm.ConnectCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy); Assert.False(vm.CanConnect); Assert.False(vm.CanChangeScenario);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null); await first;
        Assert.False(vm.IsBusy); Assert.Contains("cancelada", vm.Message);
        vm.Dispose(); vm.AddDiagnostic("ignorar"); Assert.Empty(vm.Diagnostics);
    }
}
