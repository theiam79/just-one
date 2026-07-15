namespace Party.JustOne;

/// <summary>
/// Whether each clue-giver writes two clues instead of one — the small-group variant.
/// It exists because a short-handed game is fragile: with only two writers, one duplicate
/// cancels out half the clues and an identical pair leaves the guesser nothing at all.
/// </summary>
public enum TwoCluesMode
{
    /// <summary>On when a round starts short-handed. The default.</summary>
    Auto,

    /// <summary>Always two clues each, however many are playing.</summary>
    Always,

    /// <summary>Always one clue each, even when short-handed.</summary>
    Never,
}
