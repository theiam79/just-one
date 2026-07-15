namespace Party.Flip7;

/// <summary>
/// The pure rules of Flip 7, as functions over a single player's line.
/// </summary>
/// <remarks>
/// Bust is a predicate over the tableau rather than a check inlined into the draw path, so
/// that "did this player bust" stays answerable from state alone.
/// </remarks>
public static class Flip7Rules
{
    /// <summary>Number cards needed for a Flip 7.</summary>
    public const int Flip7Count = 7;

    /// <summary>Bonus for a Flip 7.</summary>
    public const int Flip7Bonus = 15;

    /// <summary>Total needed to win. Checked only at the end of a round.</summary>
    public const int WinningScore = 200;

    /// <summary>
    /// Whether giving <paramref name="card"/> to this line would bust it: a second number card
    /// of a value already down. You cannot bust on a modifier or an action card.
    /// </summary>
    public static bool WouldBust(Tableau tableau, Card card) =>
        card is NumberCard number && tableau.HasNumber(number.Value);

    /// <summary>Whether this line holds a Flip 7.</summary>
    public static bool IsFlip7(Tableau tableau) => tableau.NumberCount >= Flip7Count;

    /// <summary>
    /// What this line banks. Order is load-bearing, per the official FAQ: sum the number cards,
    /// multiply by x2, then add the other modifiers, then the Flip 7 bonus. x2 multiplies only
    /// the number cards — never the modifiers and never the bonus.
    /// </summary>
    /// <remarks>
    /// Two x2 cards can only reach one line when the table is big enough to play with a second
    /// deck, which the published rules never contemplate. We stack them multiplicatively, so two
    /// is x4. With a single deck there is only one x2 and this is exactly the official rule.
    /// <para>
    /// A busted line scores nothing at all — its modifiers are wiped. A frozen line banks
    /// normally, modifiers included. That difference is the whole point of the two cards.
    /// </para>
    /// </remarks>
    public static int Score(Tableau tableau, bool busted)
    {
        if (busted)
        {
            return 0;
        }

        var doublings = tableau.Modifiers.Count(m => m.Kind == ModifierKind.Times2);
        var total = tableau.Numbers.Sum(n => n.Value) * (1 << doublings);
        total += tableau.Modifiers.Sum(m => m.PlusValue);

        if (IsFlip7(tableau))
        {
            total += Flip7Bonus;
        }

        return total;
    }
}
