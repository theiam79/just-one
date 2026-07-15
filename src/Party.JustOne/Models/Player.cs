namespace Party.JustOne;

public sealed class Player
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public bool IsHost { get; set; }

    /// <summary>Joined mid-game; watches until the next game starts.</summary>
    public bool IsSpectator { get; set; }

    /// <summary>
    /// Sat out by the host because they were away, rather than being skipped every round.
    /// Unlike a mid-game spectator this persists across games while they stay away, and lifts
    /// automatically as soon as they reconnect.
    /// </summary>
    public bool BenchedForInactivity { get; set; }

    /// <summary>Number of live circuits (browser tabs) for this player; maintained by the web layer.</summary>
    public int ConnectionCount { get; set; }

    public bool IsConnected => ConnectionCount > 0;
}
