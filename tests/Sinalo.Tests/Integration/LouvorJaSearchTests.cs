using System.Collections.Concurrent;
using System.IO;
using Sinalo.Application.LouvorJa;
using Sinalo.App.ViewModels;

namespace Sinalo.Tests.Integration;

public sealed class LouvorJaSearchTests
{
    private sealed class Client : ILouvorJaClient
    {
        public ConcurrentQueue<string> Queries { get; } = new();
        public Func<string, CancellationToken, Task<IReadOnlyList<LouvorJaSong>>> Search { get; set; } = (query, _) => Task.FromResult<IReadOnlyList<LouvorJaSong>>([new(1, query, "Teste", true)]);
        public Task ConnectAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task<LouvorJaState> GetStateAsync(CancellationToken token = default) => Task.FromResult(new LouvorJaState("idle", "", "", 0, 0, true, 0));
        public Task<IReadOnlyList<LouvorJaSong>> SearchAsync(string query, CancellationToken token = default)
        { Queries.Enqueue(query); return Search(query, token); }
        public Task OpenAsync(int id, LouvorJaMode mode, CancellationToken token = default) => throw new InvalidOperationException("Search must not play music.");
        public Task SendAsync(LouvorJaCommand command, int volume = 50, CancellationToken token = default) => throw new InvalidOperationException("Search must not control playback.");
    }
    private static LouvorJaIntegrationViewModel Create(Client client) => new(client, _ => { }, () => { }) { Query = "", SearchDelay = TimeSpan.FromMilliseconds(40) };
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    [Fact]
    public async Task TypingRapidly_QueriesOnlyFinalText_AndKeepsInputEnabled()
    {
        var client = new Client(); using var model = Create(client);
        await model.ConnectCommand.ExecuteAsync(null);
        model.Query = "a"; model.Query = "am"; model.Query = " amor ";
        Assert.True(model.IsSearching); Assert.True(model.CanEditSearch); Assert.False(model.IsBusy);
        await UntilAsync(() => !model.IsSearching);
        Assert.Equal("amor", Assert.Single(client.Queries));
        Assert.Equal("amor", Assert.Single(model.Songs).Name);
        Assert.Contains("encontrada", model.SearchStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateResultOrFailure_FromPreviousText_CannotOverwriteNewQuery(bool fail)
    {
        var previous = new TaskCompletionSource<IReadOnlyList<LouvorJaSong>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Search = (query, _) => query == "antiga" ? previous.Task : Task.FromResult<IReadOnlyList<LouvorJaSong>>([new(2, query, "Teste", true)]) };
        using var model = Create(client); await model.ConnectCommand.ExecuteAsync(null);
        model.Query = "antiga"; await UntilAsync(() => client.Queries.Count == 1);
        model.Query = "nova";
        Assert.Empty(model.Songs); Assert.Null(model.SelectedSong); Assert.True(model.CanEditSearch);
        if (fail) previous.SetException(new IOException("Falha antiga")); else previous.SetResult([new(1, "antiga", "Teste", true)]);
        await UntilAsync(() => !model.IsSearching);
        Assert.True(model.IsConnected); Assert.Equal("nova", Assert.Single(model.Songs).Name);
        Assert.DoesNotContain("Falha antiga", model.Message);
    }

    [Fact]
    public async Task ClearingInput_CancelsAndRemovesSelection_WithoutCallingEmptyQuery()
    {
        var client = new Client(); using var model = Create(client); await model.ConnectCommand.ExecuteAsync(null);
        model.Query = "teste"; await model.SearchCommand.ExecuteAsync(null);
        model.SelectedSong = model.Songs[0]; Assert.True(model.CanOpen);
        model.Query = "   ";
        Assert.Empty(model.Songs); Assert.Null(model.SelectedSong); Assert.False(model.CanOpen); Assert.False(model.IsSearching);
        await Task.Delay(90); Assert.Single(client.Queries); Assert.True(model.IsConnected);
    }

    [Fact]
    public async Task ManualSearch_SkipsDebounce_WithoutDuplicatingScheduledQuery()
    {
        var client = new Client(); using var model = new LouvorJaIntegrationViewModel(client, _ => { }, () => { }) { Query = "", SearchDelay = TimeSpan.FromSeconds(10) };
        await model.ConnectCommand.ExecuteAsync(null); model.Query = "123";
        await model.SearchCommand.ExecuteAsync(null);
        Assert.Equal("123", Assert.Single(client.Queries)); Assert.False(model.IsSearching);
        Assert.Equal("123", Assert.Single(model.Songs).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisconnectOrDispose_DiscardsInFlightResponse(bool dispose)
    {
        var response = new TaskCompletionSource<IReadOnlyList<LouvorJaSong>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Search = (_, _) => response.Task };
        using var model = Create(client); await model.ConnectCommand.ExecuteAsync(null);
        model.Query = "teste"; var search = model.SearchCommand.ExecuteAsync(null);
        await UntilAsync(() => client.Queries.Count == 1);
        if (dispose) model.Dispose(); else model.DisconnectCommand.Execute(null);
        response.SetResult([new(1, "Resposta atrasada", "Teste", true)]); await search;
        Assert.Empty(model.Songs); Assert.False(model.IsSearching); Assert.False(model.CanEditSearch);
    }

    [Fact]
    public async Task EmptyAndFailure_HaveVisibleOutcomes_AndStopSpinner()
    {
        var client = new Client { Search = (_, _) => Task.FromResult<IReadOnlyList<LouvorJaSong>>([]) };
        using var model = Create(client); await model.ConnectCommand.ExecuteAsync(null); model.Query = "nada";
        await model.SearchCommand.ExecuteAsync(null);
        Assert.Contains("Nenhuma música", model.SearchStatus); Assert.False(model.IsSearching); Assert.True(model.IsConnected);
        client.Search = (_, _) => throw new IOException("Servidor desligado"); model.Query = "erro";
        await model.SearchCommand.ExecuteAsync(null);
        Assert.Contains("Servidor desligado", model.SearchStatus); Assert.False(model.IsSearching); Assert.False(model.IsConnected);
    }

    [Fact]
    public async Task DisconnectedTyping_DoesNotQuery_UntilConnectionIsReady()
    {
        var client = new Client(); using var model = Create(client); model.Query = "esperança";
        await Task.Delay(90); Assert.Empty(client.Queries);
        await model.ConnectCommand.ExecuteAsync(null); await UntilAsync(() => !model.IsSearching);
        Assert.Equal("esperança", Assert.Single(client.Queries));
    }
}
