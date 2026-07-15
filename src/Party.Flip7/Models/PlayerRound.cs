namespace Party.Flip7;

public enum RoundStatus
{
    /// <summary>Still being offered Hit or Stay.</summary>
    Active,
    /// <summary>Chose to stop; banks their line.</summary>
    Stayed,
    /// <summary>Stopped by a Freeze; banks their line just the same.</summary>
    Frozen,
    /// <summary>Took a duplicate number; scores nothing.</summary>
    Busted,
}

/// <summary>One player's participation in one round.</summary>
public sealed class PlayerRound
{
    public Tableau Tableau { get; } = new();

    public RoundStatus Status { get; set; } = RoundStatus.Active;

    /// <summary>
    /// Whether this player may still act, and whether an action card may target them.
    /// </summary>
    public bool IsActive => Status == RoundStatus.Active;

    /// <summary>
    /// Whether this player banks their line when the round ends. Deliberately distinct from
    /// <see cref="IsActive"/>: a frozen or stayed player is done acting but still scores.
    /// </summary>
    public bool Scores => Status != RoundStatus.Busted;
}
