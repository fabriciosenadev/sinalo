namespace Sinalo.Application.LouvorJa;

public enum LouvorJaProcessState { Closed, Running, Unknown }
public sealed record LouvorJaProcessObservation(LouvorJaProcessState State, int? ProcessId = null, string? Detail = null);
public interface ILouvorJaProcessMonitor
{
    Task<LouvorJaProcessObservation> InspectAsync(string executable, CancellationToken token = default);
    Task StartAsync(string executable, CancellationToken token = default);
}
public enum LouvorJaConnectionState { Disconnected, Closed, Waiting, Unavailable, Connected, Lost, ManualDisconnected, Unknown }
public sealed record LouvorJaConnectionSnapshot(long Sequence, LouvorJaConnectionState Connection, string Message, LouvorJaState? Presentation = null);

public interface ILouvorJaSession : ILouvorJaClient, IAsyncDisposable
{
    LouvorJaConnectionSnapshot Current { get; }
    event Action<LouvorJaConnectionSnapshot>? Changed;
    bool ObserveContent { get; set; }
    void Configure(LouvorJaConfiguration configuration);
    void InvalidateConfiguration();
    Task StartAsync(LouvorJaConfiguration configuration, CancellationToken token = default);
    Task OpenAndConnectAsync(CancellationToken token = default);
    void Disconnect();
    void Stop();
}
