using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class DeckExhaustionTests
{
    [Test]
    public async Task Reshuffling_only_takes_back_what_was_discarded()
    {
        // Bob spends a Second Chance, putting two cards in the discard pile. Everything else is
        // still in front of somebody, so those two are all the reshuffle has to work with.
        var room = ExactDeck(
            Num(5), Num(2), Num(3),   // deal
            SecondChance,             // Bob
            Num(5),                   // Bob again: duplicate, cancelled
            Num(6));                  // one card left in the deck
        Lobby3(room);
        room.StartGame(Alice);

        room.Hit(Bob);     // Second Chance
        room.Stay(Carol);
        room.Stay(Alice);
        room.Hit(Bob);     // duplicate 5 -> Second Chance + the 5 both discarded

        await Assert.That(room.DiscardCount).IsEqualTo(2);
        await Assert.That(room.DeckCount).IsEqualTo(1);

        room.Hit(Bob);     // takes the last card, emptying the deck
        await Assert.That(room.DeckCount).IsEqualTo(0);

        room.Hit(Bob);     // forces a reshuffle of those two discards
        await Assert.That(room.DeckCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Cards_in_front_of_players_are_never_reshuffled()
    {
        var room = ExactDeck(Num(5), Num(2), Num(3), Num(6));
        Lobby3(room);
        room.StartGame(Alice);

        // Nothing has been discarded, so there is nothing to reshuffle: the three dealt cards
        // stay where they are even though the deck is nearly out.
        await Assert.That(room.DiscardCount).IsEqualTo(0);
        await Assert.That(room.DeckCount).IsEqualTo(1);

        room.Hit(Bob);  // takes the last card
        room.Stay(Carol);
        room.Stay(Alice);
        room.Stay(Bob);

        // Only now, at the end of the round, do those cards reach the discard pile — all four
        // of them, which is the whole deck.
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.DiscardCount).IsEqualTo(4);
    }

    [Test]
    public async Task An_empty_deck_and_an_empty_discard_ends_the_round()
    {
        // Three cards for the deal and nothing else. Bob asks for one that isn't there.
        var room = ExactDeck(Num(5), Num(2), Num(3));
        Lobby3(room);
        room.StartGame(Alice);

        await Assert.That(room.DeckCount).IsEqualTo(0);
        await Assert.That(room.DiscardCount).IsEqualTo(0);

        room.Hit(Bob);

        // Everyone still in banks what they were holding, rather than the round hanging.
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.Totals[Bob]).IsEqualTo(5);
        await Assert.That(room.Totals[Carol]).IsEqualTo(2);
        await Assert.That(room.Totals[Alice]).IsEqualTo(3);
    }

    [Test]
    public async Task Running_out_mid_deal_ends_the_round()
    {
        var room = ExactDeck(Num(5), Num(2));  // not even enough to deal all three
        Lobby3(room);
        room.StartGame(Alice);

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.Totals[Alice]).IsEqualTo(0);
    }

    [Test]
    public async Task Every_card_is_always_somewhere()
    {
        // Deck + discard + what is in front of players should always account for the whole deck.
        var room = Lobby3(new Flip7Room("TEST", _ => Flip7Deck.Shuffled(new Random(3))(3), new Random(3)));
        room.StartGame(Alice);

        var steps = 0;
        while (room.Phase is Flip7Phase.Dealing or Flip7Phase.Turns && steps < 500)
        {
            steps++;

            if (room.PendingChoice is { } choice)
            {
                // Mid-play a card is in flight — in nobody's hands and not yet discarded — so
                // the count is only expected to balance when the round is at rest.
                room.ChooseTarget(choice.ChooserId, room.LegalChoiceTargets[0]);
                continue;
            }

            await Assert.That(Total(room)).IsEqualTo(94);

            var current = room.Round!.CurrentPlayerId!.Value;
            if (room.LineOf(current).NumberCount >= 3)
            {
                room.Stay(current);
            }
            else
            {
                room.Hit(current);
            }
        }

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(Total(room)).IsEqualTo(94);
    }

    [Test]
    public async Task A_reshuffle_leaves_the_freeze_in_front_of_the_frozen_player()
    {
        // Force an actual reshuffle and prove the Freeze isn't swept back into the deck with it.
        // Deck: the deal, then Bob's Freeze, then one card and nothing more.
        var room = ExactDeck(Num(5), Num(2), Num(3), Freeze, Num(6));
        Lobby3(room);
        room.StartGame(Alice);

        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);   // Carol frozen; the Freeze sits in front of her

        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.DeckCount).IsEqualTo(1);
        await Assert.That(room.DiscardCount).IsEqualTo(0);

        room.Hit(Alice);                 // takes the last card, emptying the deck
        await Assert.That(room.DeckCount).IsEqualTo(0);

        // Nothing has ever been discarded, so there is nothing to reshuffle — the Freeze in
        // front of Carol is not available to it, and the round ends for want of cards.
        room.Hit(Bob);

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.LineOf(Carol).Cards).IsEmpty();
        // Every card, the Freeze included, is set aside only now the round is over.
        await Assert.That(room.DiscardCount).IsEqualTo(5);
    }

    private static int Total(Flip7Room room)
    {
        var onTable = room.Round!.Hands.Values.Sum(h => h.Tableau.Cards.Count);
        return room.DeckCount + room.DiscardCount + onTable;
    }
}
