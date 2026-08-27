namespace Sinalo.Application.Raffle;

public enum RaffleState { Ready, Animating, Completed }
public sealed record RaffleConfiguration(TimeSpan AnimationDuration);
public interface IRaffleConfigurationService
{
    Task<RaffleConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RaffleConfiguration configuration, CancellationToken cancellationToken = default);
}
public sealed record RaffleParticipant(Guid Id, string Label, bool IsDrawn);
public sealed record RaffleSnapshot(RaffleState State, string CurrentLabel, int AvailableCount, int DrawnCount, IReadOnlyList<RaffleParticipant> Participants);

public sealed class RaffleSession(Func<DateTimeOffset>? utcNow = null, Random? random = null)
{
    private readonly Func<DateTimeOffset> _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    private readonly Random _random = random ?? Random.Shared;
    private readonly List<RaffleParticipant> _participants = [];
    private DateTimeOffset? _animationEndsAt;
    private string _currentLabel = "—";
    public RaffleState State { get; private set; } = RaffleState.Ready;
    public TimeSpan AnimationDuration { get; private set; } = TimeSpan.FromSeconds(5);

    public void SetAnimationDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        AnimationDuration = duration;
    }

    public void AddName(string label) => Add(label);
    public void AddRange(int first, int last)
    {
        if (State == RaffleState.Animating) throw new InvalidOperationException("Aguarde a animação terminar.");
        foreach (var number in Enumerable.Range(Math.Min(first, last), Math.Abs(last - first) + 1)) Add(number.ToString("0000"));
    }

    public void Start()
    {
        if (State == RaffleState.Animating) return;
        if (!_participants.Any(item => !item.IsDrawn)) throw new InvalidOperationException("Não há participantes disponíveis para sortear.");
        State = RaffleState.Animating;
        _animationEndsAt = _utcNow() + AnimationDuration;
        SelectCandidate();
    }

    public RaffleSnapshot Tick()
    {
        if (State != RaffleState.Animating) return Snapshot();
        SelectCandidate();
        if (_utcNow() >= _animationEndsAt)
        {
            var winner = _participants.Single(item => item.Label == _currentLabel && !item.IsDrawn);
            Replace(winner with { IsDrawn = true });
            State = RaffleState.Completed;
            _animationEndsAt = null;
        }
        return Snapshot();
    }

    public void Restore(Guid id)
    {
        if (State == RaffleState.Animating) throw new InvalidOperationException("Aguarde a animação terminar.");
        var item = _participants.Single(item => item.Id == id);
        Replace(item with { IsDrawn = false });
    }
    public void MarkDrawn(Guid id)
    {
        if (State == RaffleState.Animating) throw new InvalidOperationException("Aguarde a animação terminar.");
        var item = _participants.Single(item => item.Id == id);
        Replace(item with { IsDrawn = true });
    }
    public void ResetDisplay() { if (State != RaffleState.Animating) { State = RaffleState.Ready; _currentLabel = "—"; } }
    public void Restart() { if (State == RaffleState.Animating) throw new InvalidOperationException("Aguarde a animação terminar."); for (var i = 0; i < _participants.Count; i++) _participants[i] = _participants[i] with { IsDrawn = false }; ResetDisplay(); }
    public void Clear() { if (State == RaffleState.Animating) throw new InvalidOperationException("Aguarde a animação terminar."); _participants.Clear(); ResetDisplay(); }
    public RaffleSnapshot Snapshot() => new(State, _currentLabel, _participants.Count(item => !item.IsDrawn), _participants.Count(item => item.IsDrawn), _participants.ToArray());

    private void Add(string label)
    {
        if (State == RaffleState.Animating) throw new InvalidOperationException("Aguarde a animação terminar.");
        label = label.Trim();
        if (string.IsNullOrWhiteSpace(label) || _participants.Any(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase))) return;
        _participants.Add(new RaffleParticipant(Guid.NewGuid(), label, false));
    }
    private void SelectCandidate()
    {
        var available = _participants.Where(item => !item.IsDrawn).ToArray();
        _currentLabel = available[_random.Next(available.Length)].Label;
    }
    private void Replace(RaffleParticipant replacement) { var index = _participants.FindIndex(item => item.Id == replacement.Id); _participants[index] = replacement; }
}
