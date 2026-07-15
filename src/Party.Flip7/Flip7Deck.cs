namespace Party.Flip7;

/// <summary>
/// Builds Flip 7 decks. The deck is data — a flat list of cards — so scaling to more players
/// is just concatenating copies, and a test can hand the room an exact card sequence instead.
/// </summary>
public static class Flip7Deck
{
    public const int CardsPerDeck = 94;

    /// <summary>
    /// Players supported by one deck before a second is recommended. The rulebook's own
    /// threshold: "If playing with more than 18 people, we recommend playing with a second deck."
    /// </summary>
    public const int PlayersPerDeck = 18;

    /// <summary>How many copies of the deck a table of this size plays with.</summary>
    public static int DecksFor(int players) => Math.Max(1, (players + PlayersPerDeck - 1) / PlayersPerDeck);

    /// <summary>
    /// One full deck, unshuffled: 79 number cards (count equals face value, except one 0),
    /// 6 modifiers (one each), and 9 action cards (three each).
    /// </summary>
    public static IReadOnlyList<Card> Single()
    {
        var cards = new List<Card>(CardsPerDeck) { new NumberCard(0) };

        for (var value = 1; value <= NumberCard.MaxValue; value++)
        {
            for (var i = 0; i < value; i++)
            {
                cards.Add(new NumberCard(value));
            }
        }

        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            cards.Add(new ModifierCard(kind));
        }

        foreach (var kind in Enum.GetValues<ActionKind>())
        {
            for (var i = 0; i < 3; i++)
            {
                cards.Add(new ActionCard(kind));
            }
        }

        return cards;
    }

    /// <summary>N copies of the deck, unshuffled.</summary>
    public static IReadOnlyList<Card> Copies(int decks) =>
        [.. Enumerable.Range(0, decks).SelectMany(_ => Single())];

    /// <summary>The production deck source: enough copies for the table, shuffled.</summary>
    public static Func<int, IReadOnlyList<Card>> Shuffled(Random rng) => players =>
    {
        var cards = Copies(DecksFor(players)).ToArray();
        Shuffle(cards, rng);
        return cards;
    };

    public static void Shuffle<T>(IList<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
