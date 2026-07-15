namespace Party.Flip7;

public enum ChoiceKind
{
    /// <summary>Who to play a Freeze or a Flip Three on. Any active player, yourself included.</summary>
    ActionTarget,
    /// <summary>Who to hand a surplus Second Chance to. Another active player who hasn't got one.</summary>
    SecondChanceRecipient,
}

/// <summary>
/// A decision the round is blocked on. Nothing else happens until it is answered — a dealt
/// action card is resolved before the next player is offered Hit or Stay, and the deal itself
/// pauses mid-way if a card needs a target.
/// </summary>
public sealed record PendingChoice(Guid ChooserId, Card Card, ChoiceKind Kind);
