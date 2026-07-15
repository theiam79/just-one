using Party.Core;

namespace Party.Flip7.Tests;

/// <summary>
/// Builders for Flip 7 rooms in known states.
/// </summary>
/// <remarks>
/// Rooms are built on a <em>stacked</em> deck — the exact card sequence a test wants, dealt in
/// order — rather than a seeded shuffle. Almost every rule worth testing is about a specific
/// card reaching a specific player ("deal Bob a second 5", "hand Alice a Freeze while she holds
/// a Second Chance"), and those states aren't reachable by hunting for a lucky seed.
/// <para>
/// Players must be connected. A disconnected player is auto-played by the engine so a dropped
/// circuit can't stall a turn game, which would otherwise play the whole round for you.
/// </para>
/// </remarks>
internal static class TestGame
{
    public static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid Dave = Guid.Parse("00000000-0000-0000-0000-000000000004");

    public static NumberCard Num(int value) => new(value);
    public static ModifierCard Mod(ModifierKind kind) => new(kind);
    public static ActionCard Act(ActionKind kind) => new(kind);

    public static readonly ActionCard Freeze = Act(ActionKind.Freeze);
    public static readonly ActionCard FlipThree = Act(ActionKind.FlipThree);
    public static readonly ActionCard SecondChance = Act(ActionKind.SecondChance);
    public static readonly ModifierCard Times2 = Mod(ModifierKind.Times2);

    /// <summary>
    /// A room whose deck is exactly <paramref name="cards"/>, in order, followed by an endless
    /// run of harmless filler so a test never trips over the deck running out unless it means to.
    /// </summary>
    public static Flip7Room Stacked(params Card[] cards) => Stacked(cards, filler: true);

    /// <summary>A room whose deck is exactly <paramref name="cards"/> and nothing else.</summary>
    public static Flip7Room ExactDeck(params Card[] cards) => Stacked(cards, filler: false);

    private static Flip7Room Stacked(IEnumerable<Card> cards, bool filler)
    {
        var deck = cards.ToList();
        if (filler)
        {
            // Zeroes: they never bust anyone (only one can be held) and are worth nothing, so
            // they can't quietly change what a test is measuring.
            deck.AddRange(Enumerable.Repeat(new NumberCard(0), 200));
        }

        return new Flip7Room("TEST", _ => deck, new Random(42));
    }

    /// <summary>Alice (host), Bob and Carol in the lobby, all connected.</summary>
    public static Flip7Room Lobby3(Flip7Room room)
    {
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        return room;
    }

    /// <summary>
    /// Three players, game started. Alice is the dealer (seat 0), so the deal runs Bob, Carol,
    /// Alice — and turns then run in that same order.
    /// </summary>
    public static Flip7Room Started3(params Card[] cards)
    {
        var room = Lobby3(Stacked(cards));
        room.StartGame(Alice);
        return room;
    }

    public static Tableau LineOf(this Flip7Room room, Guid id) => room.Round![id].Tableau;

    public static RoundStatus StatusOf(this Flip7Room room, Guid id) => room.Round![id].Status;

    public static int[] NumbersOf(this Flip7Room room, Guid id) =>
        [.. room.LineOf(id).Numbers.Select(n => n.Value)];

    public static GameRuleException ExpectRuleError(Action action)
    {
        try
        {
            action();
        }
        catch (GameRuleException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected a GameRuleException, but the action succeeded.");
    }
}
