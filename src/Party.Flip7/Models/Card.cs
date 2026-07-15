namespace Party.Flip7;

/// <summary>
/// A card in the Flip 7 deck. A closed hierarchy: every card is a number, a modifier, or an
/// action. Cards are records, so two cards of the same face are interchangeable — nothing in
/// the rules distinguishes one 5 from another 5.
/// </summary>
public abstract record Card;

/// <summary>A number card. Duplicates in one line bust; seven distinct ones are a Flip 7.</summary>
public sealed record NumberCard(int Value) : Card
{
    public const int MinValue = 0;
    public const int MaxValue = 12;
}

public enum ModifierKind
{
    Plus2,
    Plus4,
    Plus6,
    Plus8,
    Plus10,
    /// <summary>Doubles the sum of the number cards only — not the modifiers, not the Flip 7 bonus.</summary>
    Times2,
}

public sealed record ModifierCard(ModifierKind Kind) : Card
{
    /// <summary>What this modifier adds to the score. Zero for <see cref="ModifierKind.Times2"/>, which multiplies instead.</summary>
    public int PlusValue => Kind switch
    {
        ModifierKind.Plus2 => 2,
        ModifierKind.Plus4 => 4,
        ModifierKind.Plus6 => 6,
        ModifierKind.Plus8 => 8,
        ModifierKind.Plus10 => 10,
        _ => 0,
    };
}

public enum ActionKind
{
    /// <summary>Target banks their points and is out of the round.</summary>
    Freeze,
    /// <summary>Target is dealt the next three cards, one at a time.</summary>
    FlipThree,
    /// <summary>Held; cancels one bust, then is discarded along with the duplicate.</summary>
    SecondChance,
}

public sealed record ActionCard(ActionKind Kind) : Card;
