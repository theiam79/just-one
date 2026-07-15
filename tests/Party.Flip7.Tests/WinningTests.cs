using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class WinningTests
{
    /// <summary>
    /// Sixteen cards that bank exactly <c>194 + finalPlus</c>: six numbers totalling 57 (one of
    /// them dealt), doubled to 114, then eight +10s and one last modifier. Six numbers, so no
    /// Flip 7 bonus muddies the arithmetic.
    /// </summary>
    private static List<Card> BigLine(ModifierKind last) =>
    [
        Num(11), Num(10), Num(9), Num(8), Num(7),
        Times2,
        Mod(ModifierKind.Plus10), Mod(ModifierKind.Plus10), Mod(ModifierKind.Plus10), Mod(ModifierKind.Plus10),
        Mod(ModifierKind.Plus10), Mod(ModifierKind.Plus10), Mod(ModifierKind.Plus10), Mod(ModifierKind.Plus10),
        Mod(last),
    ];

    /// <summary>
    /// Bob and Carol each build a big line in the same round while Alice bows out immediately.
    /// A crafted stack, not a legal deck — nothing here needs one.
    /// </summary>
    private static Flip7Room BigRound(ModifierKind bobLast, ModifierKind carolLast)
    {
        var bob = BigLine(bobLast);
        var carol = BigLine(carolLast);

        var deck = new List<Card> { Num(12), Num(12), Num(1) };  // the deal: Bob, Carol, Alice
        for (var i = 0; i < bob.Count; i++)
        {
            deck.Add(bob[i]);
            deck.Add(carol[i]);
        }

        var room = Lobby3(new Flip7Room("TEST", _ => deck, new Random(42)));
        room.StartGame(Alice);

        room.Hit(Bob);
        room.Hit(Carol);
        room.Stay(Alice);
        for (var i = 1; i < bob.Count; i++)
        {
            room.Hit(Bob);
            room.Hit(Carol);
        }

        room.Stay(Bob);
        room.Stay(Carol);
        return room;
    }

    [Test]
    public async Task Nobody_wins_below_the_target()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        await Assert.That(room.Winner).IsNull();
        room.NextRound(Alice);
        await Assert.That(room.Phase).IsNotEqualTo(Flip7Phase.GameOver);
    }

    [Test]
    public async Task Reaching_the_target_alone_wins()
    {
        // Bob banks 200, Carol 198.
        var room = BigRound(ModifierKind.Plus6, ModifierKind.Plus4);

        await Assert.That(room.Totals[Bob]).IsEqualTo(200);
        await Assert.That(room.Totals[Carol]).IsEqualTo(198);
        await Assert.That(room.Winner).IsEqualTo(Bob);

        room.NextRound(Alice);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.GameOver);
    }

    [Test]
    public async Task The_highest_score_wins_even_if_someone_else_crossed_first()
    {
        var room = BigRound(ModifierKind.Plus4, ModifierKind.Plus10);

        // Both are past the target; Carol is simply higher.
        await Assert.That(room.Totals[Bob]).IsEqualTo(198);
        await Assert.That(room.Totals[Carol]).IsEqualTo(204);
        await Assert.That(room.Winner).IsEqualTo(Carol);
    }

    [Test]
    public async Task A_tie_at_the_top_is_not_a_win_and_everyone_plays_on()
    {
        // Level on 200. The publisher's ruling is that all players — not just the tied ones —
        // play another round, so there is no winner yet.
        var room = BigRound(ModifierKind.Plus6, ModifierKind.Plus6);

        await Assert.That(room.Totals[Bob]).IsEqualTo(200);
        await Assert.That(room.Totals[Carol]).IsEqualTo(200);
        await Assert.That(room.Winner).IsNull();

        room.NextRound(Alice);
        await Assert.That(room.Phase).IsNotEqualTo(Flip7Phase.GameOver);
        await Assert.That(room.Round!.RoundNumber).IsEqualTo(2);
        // Alice is still in it, well behind though she is.
        await Assert.That(room.Round.Order).Contains(Alice);
    }

    [Test]
    public async Task Only_the_host_can_move_the_game_on()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        var error = ExpectRuleError(() => room.NextRound(Bob));
        await Assert.That(error.Message).Contains("Only the host");
    }

    [Test]
    public async Task Anyone_may_drive_when_the_host_is_away()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        room.PlayerDisconnected(Alice);

        room.NextRound(Bob);
        await Assert.That(room.Round!.RoundNumber).IsEqualTo(2);
    }
}
