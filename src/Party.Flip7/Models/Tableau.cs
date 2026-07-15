namespace Party.Flip7;

/// <summary>
/// The cards face up in front of one player this round — their "line".
/// </summary>
/// <remarks>
/// Supports removal, not just appending, because Second Chance takes its own card and the
/// duplicate back out of the line. Everything in front of a player lives here, including
/// cards that never score (a Freeze stays in front of the player it froze until the round
/// ends, and a held Second Chance is not worth points), so that end-of-round cleanup and
/// mid-round reshuffles can reason about where every card physically is.
/// </remarks>
public sealed class Tableau
{
    private readonly List<Card> _cards = [];

    public IReadOnlyList<Card> Cards => _cards;

    public void Add(Card card) => _cards.Add(card);

    public bool Remove(Card card) => _cards.Remove(card);

    public bool IsEmpty => _cards.Count == 0;

    public IEnumerable<NumberCard> Numbers => _cards.OfType<NumberCard>();

    public IEnumerable<ModifierCard> Modifiers => _cards.OfType<ModifierCard>();

    /// <summary>
    /// How many number cards are down. A line can never hold a duplicate — you bust or spend a
    /// Second Chance first — so this is also the count of *distinct* numbers, which is what
    /// Flip 7 is measured against.
    /// </summary>
    public int NumberCount => Numbers.Count();

    public bool HasNumber(int value) => Numbers.Any(n => n.Value == value);

    public bool HasSecondChance => _cards.Any(c => c is ActionCard { Kind: ActionKind.SecondChance });

    public Card? SecondChance => _cards.FirstOrDefault(c => c is ActionCard { Kind: ActionKind.SecondChance });

    public void Clear() => _cards.Clear();
}
