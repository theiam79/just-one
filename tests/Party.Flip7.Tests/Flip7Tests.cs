using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class Flip7Tests
{
    /// <summary>
    /// Bob stays straight away, so Carol and Alice alternate. Carol collects a seventh number
    /// and everything stops.
    /// </summary>
    private static Flip7Room SevenForCarol() =>
        Started3(
            Num(1), Num(2), Num(3),   // deal: Bob 1, Carol 2, Alice 3
            Num(4), Num(9),           // Carol 4, Alice 9
            Num(5), Num(10),          // Carol 5, Alice 10
            Num(6), Num(11),          // Carol 6, Alice 11
            Num(7), Num(12),          // Carol 7, Alice 12
            Num(8), Num(0),           // Carol 8, Alice 0
            Num(9));                  // Carol's seventh

    private static void PlayToFlip7(Flip7Room room)
    {
        room.Stay(Bob);
        for (var i = 0; i < 5; i++)
        {
            room.Hit(Carol);
            room.Hit(Alice);
        }

        room.Hit(Carol);
    }

    [Test]
    public async Task Seven_numbers_ends_the_round_for_everyone()
    {
        var room = SevenForCarol();
        PlayToFlip7(room);

        await Assert.That(room.Round!.Flip7PlayerId).IsEqualTo(Carol);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }

    [Test]
    public async Task The_flip7_player_scores_the_bonus()
    {
        var room = SevenForCarol();
        PlayToFlip7(room);

        // 2+4+5+6+7+8+9 = 41, +15
        await Assert.That(room.Totals[Carol]).IsEqualTo(56);
    }

    [Test]
    public async Task Everyone_else_banks_what_they_were_holding()
    {
        var room = SevenForCarol();
        PlayToFlip7(room);

        await Assert.That(room.Totals[Bob]).IsEqualTo(1);
        // 3+9+10+11+12+0
        await Assert.That(room.Totals[Alice]).IsEqualTo(45);
    }

    [Test]
    public async Task A_flip7_reached_mid_flip_three_stops_the_flips()
    {
        // Carol has six numbers and turns up a Flip Three, which she plays on herself. The
        // first card gives her a seventh, so the other two are never flipped.
        var room = Started3(
            Num(1), Num(2), Num(3),
            Num(4), Num(9),
            Num(5), Num(10),
            Num(6), Num(11),
            Num(7), Num(12),
            Num(8), Num(0),
            FlipThree,
            Num(9), Num(10), Num(11));

        room.Stay(Bob);
        for (var i = 0; i < 5; i++)
        {
            room.Hit(Carol);
            room.Hit(Alice);
        }

        var deckBefore = room.DeckCount;
        room.Hit(Carol); // the Flip Three; Alice is the only other active player

        // Carol is not the only active player, so she must pick — and picks herself.
        room.ChooseTarget(Carol, Carol);

        await Assert.That(room.Round!.Flip7PlayerId).IsEqualTo(Carol);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        // The Flip Three came off the deck, then exactly one flip.
        await Assert.That(room.DeckCount).IsEqualTo(deckBefore - 2);
    }

    [Test]
    public async Task Zero_counts_towards_the_seven()
    {
        var room = Started3(
            Num(0), Num(2), Num(3),   // Bob starts on a zero
            Num(1), Num(9),
            Num(4), Num(10),
            Num(5), Num(11),
            Num(6), Num(12),
            Num(7), Num(8));

        // Bob leads the turn order, so he takes one before the others bow out.
        room.Hit(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        for (var i = 0; i < 4; i++)
        {
            room.Hit(Bob);
        }

        // Six numbers so far, the zero he started on among them.
        await Assert.That(room.LineOf(Bob).NumberCount).IsEqualTo(6);
        await Assert.That(room.NumbersOf(Bob)).Contains(0);

        room.Hit(Bob);

        // The zero was one of the seven: 0+1+9+4+10+5+11 = 40, +15 for the bonus.
        await Assert.That(room.Round!.Flip7PlayerId).IsEqualTo(Bob);
        await Assert.That(room.Totals[Bob]).IsEqualTo(55);
    }
}
