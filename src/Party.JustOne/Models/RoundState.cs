namespace Party.JustOne;

public sealed class RoundState
{
    public required int RoundNumber { get; init; }
    public required Guid GuesserId { get; init; }
    public required Card Card { get; init; }
    public int? ChosenNumber { get; set; }
    public string? MysteryWord { get; set; }

    /// <summary>Players who must submit a clue this round; fixed when the number is picked.</summary>
    public HashSet<Guid> ExpectedWriters { get; } = [];

    /// <summary>Expected writers whose clue was skipped (e.g. they disconnected).</summary>
    public HashSet<Guid> SkippedWriters { get; } = [];

    /// <summary>
    /// How many clues each writer owes this round; fixed when the number is picked, so the
    /// rule can't change under someone mid-round. Two when playing the small-group variant.
    /// </summary>
    public int CluesPerWriter { get; set; } = 1;

    /// <summary>Each writer's clues, keyed by author. A writer submits all of theirs at once.</summary>
    public Dictionary<Guid, List<Clue>> Clues { get; } = [];
    public string? Guess { get; set; }
    public RoundOutcome? Outcome { get; set; }

    /// <summary>
    /// When the group's shared countdown runs out, or null if nobody started one.
    /// Purely advisory — nothing happens automatically when it passes.
    /// </summary>
    public DateTimeOffset? TimerDeadline { get; set; }

    /// <summary>
    /// The phase <see cref="TimerDeadline"/> was started for, so a countdown left over from
    /// an earlier phase stops being shown once the round moves on.
    /// </summary>
    public GamePhase? TimerPhase { get; set; }
}
