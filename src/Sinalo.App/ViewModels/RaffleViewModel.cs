using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Raffle;

namespace Sinalo.App.ViewModels;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed partial class RaffleViewModel : ObservableObject
{
    private readonly RaffleSession _session;
    public RaffleViewModel(RaffleSession session, RaffleConfiguration configuration)
    {
        _session = session; _session.SetAnimationDuration(configuration.AnimationDuration);
        animationSecondsText = ((int)configuration.AnimationDuration.TotalSeconds).ToString(); Refresh();
    }
    [ObservableProperty] private string nameToAdd = string.Empty;
    [ObservableProperty] private string rangeStart = string.Empty;
    [ObservableProperty] private string rangeEnd = string.Empty;
    [ObservableProperty] private string animationSecondsText = "5";
    [ObservableProperty] private string currentWinner = "—";
    [ObservableProperty] private string statusLabel = "Pronto para sortear";
    [ObservableProperty] private int availableCount;
    [ObservableProperty] private int drawnCount;
    [ObservableProperty] private bool isAnimating;
    public ObservableCollection<RaffleParticipant> Available { get; } = [];
    public ObservableCollection<RaffleParticipant> Drawn { get; } = [];
    public RaffleConfiguration Configuration => new(TimeSpan.FromSeconds(ParseSeconds()));
    public void AddName() { _session.AddName(NameToAdd); NameToAdd = string.Empty; Refresh(); }
    public void AddRange() { _session.AddRange(int.Parse(RangeStart), string.IsNullOrWhiteSpace(RangeEnd) ? int.Parse(RangeStart) : int.Parse(RangeEnd)); RangeStart = RangeEnd = string.Empty; Refresh(); }
    public void Start() { _session.SetAnimationDuration(Configuration.AnimationDuration); _session.Start(); Refresh(); }
    public void Tick() { _session.Tick(); Refresh(); }
    public void ResetDisplay() { _session.ResetDisplay(); Refresh(); }
    public void Restart() { _session.Restart(); Refresh(); }
    public void Clear() { _session.Clear(); Refresh(); }
    public void Restore(Guid id) { _session.Restore(id); Refresh(); }
    public void MarkDrawn(Guid id) { _session.MarkDrawn(id); Refresh(); }
    public void Refresh()
    {
        var snapshot = _session.Snapshot(); CurrentWinner = snapshot.CurrentLabel; AvailableCount = snapshot.AvailableCount; DrawnCount = snapshot.DrawnCount; IsAnimating = snapshot.State == RaffleState.Animating;
        StatusLabel = snapshot.State == RaffleState.Animating ? "Sorteando..." : snapshot.State == RaffleState.Completed ? "Sorteio concluído" : "Pronto para sortear";
        Available.Clear(); foreach (var item in snapshot.Participants.Where(item => !item.IsDrawn)) Available.Add(item);
        Drawn.Clear(); foreach (var item in snapshot.Participants.Where(item => item.IsDrawn).Reverse()) Drawn.Add(item);
    }
    private int ParseSeconds() => int.TryParse(AnimationSecondsText, out var seconds) && seconds > 0 ? seconds : throw new FormatException("Informe uma duração de animação maior que zero.");
}
