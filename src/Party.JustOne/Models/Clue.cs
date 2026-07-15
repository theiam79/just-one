namespace Party.JustOne;

public sealed class Clue
{
    public required Guid AuthorId { get; init; }
    public required string Text { get; init; }
    public required string Normalized { get; init; }

    /// <summary>Cancelled because it duplicated another clue (or the mystery word). Cannot be reinstated.</summary>
    public bool AutoCancelled { get; set; }

    /// <summary>Cancelled by the clue-writers' judgment (word variants, same family, etc.).</summary>
    public bool ManuallyCancelled { get; set; }

    public bool Visible => !AutoCancelled && !ManuallyCancelled;
}
