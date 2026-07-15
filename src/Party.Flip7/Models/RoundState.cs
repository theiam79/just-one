namespace Party.Flip7;

/// <summary>A Flip Three part-way through its three flips.</summary>
internal sealed class FlipThreeState
{
    public required Guid TargetId { get; init; }
    public int Remaining { get; set; } = 3;

    /// <summary>
    /// Freeze and Flip Three cards turned up during the flips. They are held here rather than
    /// resolved on the spot: they take effect only once all three cards are down, and only if
    /// the flipper neither busted nor hit a Flip 7.
    /// </summary>
    public List<Card> SetAside { get; } = [];
}

public sealed class RoundState
{
    public required int RoundNumber { get; init; }

    /// <summary>
    /// Who is playing this round and in what order, fixed when the round starts so that a
    /// roster change mid-round cannot move the goalposts for anyone already playing.
    /// Ordered as the deal runs: the seat after the dealer first, the dealer last.
    /// </summary>
    public required IReadOnlyList<Guid> Order { get; init; }

    public required Guid DealerId { get; init; }

    public Dictionary<Guid, PlayerRound> Hands { get; } = [];

    /// <summary>Who is being offered Hit or Stay, or null when the round isn't waiting on that.</summary>
    public Guid? CurrentPlayerId { get; set; }

    /// <summary>Who hit a Flip 7, if anyone. Set the moment it happens; ends the round for everyone.</summary>
    public Guid? Flip7PlayerId { get; set; }

    public PlayerRound this[Guid id] => Hands[id];

    public IEnumerable<Guid> ActivePlayers => Order.Where(id => Hands[id].IsActive);
}
