namespace JustOne.Core;

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

    public Dictionary<Guid, Clue> Clues { get; } = [];
    public string? Guess { get; set; }
    public RoundOutcome? Outcome { get; set; }
}
