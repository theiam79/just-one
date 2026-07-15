using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

/// <summary>
/// Flip 7 asks one player at a time and nobody else can move until they answer, so a dropped
/// circuit would otherwise hang the whole table. The engine plays for them: stay if that's legal,
/// hit if it isn't. Staying banks what they have and can never bust them, so it costs them
/// nothing they had.
/// </summary>
public class DisconnectTests
{
    [Test]
    public async Task Dropping_on_your_turn_stays_for_you()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);

        room.PlayerDisconnected(Bob);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Stayed);
        await Assert.That(room.Round.CurrentPlayerId).IsEqualTo(Carol);
    }

    [Test]
    public async Task The_round_is_never_left_waiting_on_someone_who_has_gone()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerDisconnected(Bob);
        room.PlayerDisconnected(Carol);
        room.PlayerDisconnected(Alice);

        // Everyone banked their card and the round finished on its own.
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.Totals[Bob]).IsEqualTo(1);
        await Assert.That(room.Totals[Carol]).IsEqualTo(2);
        await Assert.That(room.Totals[Alice]).IsEqualTo(3);
    }

    [Test]
    public async Task Someone_with_nothing_in_front_of_them_hits_instead()
    {
        // Bob plays his Freeze on Carol, leaving himself empty-handed, and then drops. Staying
        // isn't legal with no cards, so hitting is the only move left. (Carol is frozen before
        // the deal reaches her, so she gets no card and Alice takes the 2.)
        var room = Started3(Freeze, Num(2), Num(3), Num(9));
        room.ChooseTarget(Bob, Carol);
        await Assert.That(room.LineOf(Bob).IsEmpty).IsTrue();

        room.PlayerDisconnected(Bob);

        // He was dealt one rather than being skipped, and is still in.
        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 3 });
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);

        // Now that he holds something, his next turn stays instead.
        room.Stay(Alice);
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Stayed);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }

    [Test]
    public async Task An_absent_player_plays_their_action_card_on_themselves()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);
        await Assert.That(room.PendingChoice!.ChooserId).IsEqualTo(Bob);

        room.PlayerDisconnected(Bob);

        // Nobody else gets picked on for them.
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.StatusOf(Alice)).IsEqualTo(RoundStatus.Active);
    }

    [Test]
    public async Task Coming_back_puts_you_in_charge_of_your_own_turn_again()
    {
        var room = Started3(Num(1), Num(2), Num(3), Num(9), Num(8), Num(7), Num(6));
        room.Hit(Bob);
        room.PlayerDisconnected(Carol);   // Carol is auto-stayed
        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Stayed);

        room.PlayerConnected(Carol);
        // She is still out for this round — the decision was made — but she is back for the next.
        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Stayed);
        room.Stay(Alice);
        room.Stay(Bob);
        room.NextRound(Alice);
        await Assert.That(room.Round!.Order).Contains(Carol);
    }

    [Test]
    public async Task A_second_tab_keeps_you_present()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerConnected(Bob);       // two circuits open
        room.PlayerDisconnected(Bob);    // one closes

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
    }
}
