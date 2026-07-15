using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

/// <summary>
/// What happens to a round when the roster moves under it. A turn game can't shrug these off:
/// there is always exactly one player everyone else is waiting on.
/// </summary>
public class RosterChurnTests
{
    private static Flip7Room Lobby4(params Card[] cards)
    {
        var room = Stacked(cards);
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.Join(Dave, "Dave");
        foreach (var id in new[] { Alice, Bob, Carol, Dave })
        {
            room.PlayerConnected(id);
        }

        return room;
    }

    [Test]
    public async Task The_dealer_moves_on_by_identity_when_a_seat_disappears()
    {
        // Seats are [Alice, Bob, Carol, Dave] and Alice deals. Come round two Bob should have
        // it — and still should even though Alice has gone and every seat has shuffled up.
        var room = Lobby4(Num(1), Num(2), Num(3), Num(4));
        room.StartGame(Alice);
        await Assert.That(room.Round!.DealerId).IsEqualTo(Alice);

        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Dave);
        room.Stay(Alice);
        room.Leave(Alice);
        room.NextRound(Bob);

        await Assert.That(room.Round!.DealerId).IsEqualTo(Bob);
        await Assert.That(room.Round.Order).IsEquivalentTo(new[] { Carol, Dave, Bob });
    }

    [Test]
    public async Task The_dealer_keeps_rotating_forwards_past_a_departed_dealer()
    {
        var room = Lobby4(Num(1), Num(2), Num(3), Num(4));
        room.StartGame(Alice);

        // Round 2: Bob deals.
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Dave);
        room.Stay(Alice);
        room.NextRound(Alice);
        await Assert.That(room.Round!.DealerId).IsEqualTo(Bob);

        // Bob deals, then leaves. Round 3 should land on Carol, not wrap back to the start.
        room.Stay(Carol);
        room.Stay(Dave);
        room.Stay(Alice);
        room.Stay(Bob);
        room.Leave(Bob);
        room.NextRound(Alice);

        await Assert.That(room.Round!.DealerId).IsEqualTo(Carol);
    }

    [Test]
    public async Task A_card_with_nobody_left_to_play_it_on_is_discarded_rather_than_stalling()
    {
        // Bob turns up a surplus Second Chance and is asked who gets it. Before he answers, both
        // candidates go. He can't name anyone — and mustn't be left holding an unanswerable
        // question while the round waits on him forever.
        var room = Lobby4(
            SecondChance, Num(2), Num(3), Num(4),   // deal: Bob, Carol, Dave, Alice
            Num(9), Num(8), Num(7), Num(6),          // one each round the table
            SecondChance);                           // and Bob's second
        room.StartGame(Alice);

        room.Hit(Bob);
        room.Hit(Carol);
        room.Hit(Dave);
        room.Hit(Alice);
        room.Hit(Bob);

        await Assert.That(room.PendingChoice!.Kind).IsEqualTo(ChoiceKind.SecondChanceRecipient);
        await Assert.That(room.PendingChoice.ChooserId).IsEqualTo(Bob);

        room.Leave(Carol);
        room.Leave(Dave);
        room.Leave(Alice);

        // The card is gone and Bob has his turn back.
        await Assert.That(room.PendingChoice).IsNull();
        room.Stay(Bob);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }

    [Test]
    public async Task A_stranded_card_does_not_crash_when_its_owner_also_drops()
    {
        var room = Lobby4(
            SecondChance, Num(2), Num(3), Num(4),
            Num(9), Num(8), Num(7), Num(6),
            SecondChance);
        room.StartGame(Alice);

        room.Hit(Bob);
        room.Hit(Carol);
        room.Hit(Dave);
        room.Hit(Alice);
        room.Hit(Bob);
        room.Leave(Carol);
        room.Leave(Dave);
        room.Leave(Alice);

        // Bob is the last one here, and then he isn't.
        room.PlayerDisconnected(Bob);

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }

    [Test]
    public async Task Everyone_leaving_mid_game_does_not_throw_a_raw_exception()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        room.Leave(Bob);
        room.Leave(Carol);
        room.Leave(Alice);

        // A rule error the web layer can show, rather than an ArgumentOutOfRangeException.
        var error = ExpectRuleError(() => room.NextRound(Alice));
        await Assert.That(error.Message).Contains("nobody left");
    }

    [Test]
    public async Task A_round_is_scored_once_even_when_it_ends_itself()
    {
        // Running out of cards ends the round from inside the pump; the totals must not be
        // added up twice on the way out.
        var room = ExactDeck(Num(5), Num(2), Num(3));
        Lobby3(room);
        room.StartGame(Alice);
        room.Hit(Bob);

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.Totals[Bob]).IsEqualTo(5);
        await Assert.That(room.Totals[Carol]).IsEqualTo(2);
        await Assert.That(room.Totals[Alice]).IsEqualTo(3);
    }
}
