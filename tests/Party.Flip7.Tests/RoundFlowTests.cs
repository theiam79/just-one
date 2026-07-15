using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class RoundFlowTests
{
    [Test]
    public async Task Deal_starts_left_of_the_dealer_and_ends_with_them()
    {
        // Alice is the dealer, so the deal runs Bob, Carol, Alice.
        var room = Started3(Num(1), Num(2), Num(3));

        await Assert.That(room.Round!.DealerId).IsEqualTo(Alice);
        await Assert.That(room.Round.Order).IsEquivalentTo(new[] { Bob, Carol, Alice });
        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 1 });
        await Assert.That(room.NumbersOf(Carol)).IsEquivalentTo(new[] { 2 });
        await Assert.That(room.NumbersOf(Alice)).IsEquivalentTo(new[] { 3 });
    }

    [Test]
    public async Task Turns_begin_left_of_the_dealer()
    {
        var room = Started3(Num(1), Num(2), Num(3));

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.Turns);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
    }

    [Test]
    public async Task Dealer_rotates_one_seat_each_round()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        room.NextRound(Alice);

        await Assert.That(room.Round!.DealerId).IsEqualTo(Bob);
        await Assert.That(room.Round.Order).IsEquivalentTo(new[] { Carol, Alice, Bob });
    }

    [Test]
    public async Task Hit_takes_the_next_card_and_passes_the_turn()
    {
        var room = Started3(Num(1), Num(2), Num(3), Num(9));
        room.Hit(Bob);

        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 1, 9 });
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Carol);
    }

    [Test]
    public async Task Only_the_current_player_may_act()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        var error = ExpectRuleError(() => room.Hit(Carol));
        await Assert.That(error.Message).Contains("not your turn");
    }

    [Test]
    public async Task A_duplicate_number_busts()
    {
        var room = Started3(Num(1), Num(2), Num(3), Num(1));
        room.Hit(Bob);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Busted);
    }

    [Test]
    public async Task Busting_scores_nothing_even_with_modifiers()
    {
        var room = Started3(Num(5), Num(2), Num(3), Mod(ModifierKind.Plus10), Num(5));
        room.Hit(Bob); // +10
        room.Stay(Carol);
        room.Stay(Alice);
        room.Hit(Bob); // second 5 -> bust

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Busted);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        await Assert.That(room.Totals[Bob]).IsEqualTo(0);
    }

    [Test]
    public async Task A_busted_player_is_skipped()
    {
        var room = Started3(Num(1), Num(2), Num(3), Num(1));
        room.Hit(Bob); // bust
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Carol);
    }

    [Test]
    public async Task Cannot_bust_on_a_modifier()
    {
        var room = Started3(Num(1), Num(2), Num(3), Mod(ModifierKind.Plus2), Mod(ModifierKind.Plus2));
        room.Hit(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        room.Hit(Bob);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
    }

    [Test]
    public async Task Staying_banks_the_line()
    {
        var room = Started3(Num(4), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        await Assert.That(room.Totals[Bob]).IsEqualTo(4);
        await Assert.That(room.Totals[Carol]).IsEqualTo(2);
        await Assert.That(room.Totals[Alice]).IsEqualTo(3);
    }

    [Test]
    public async Task Round_ends_when_nobody_is_active()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.Turns);
        room.Stay(Alice);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }

    [Test]
    public async Task A_player_with_no_cards_cannot_stay()
    {
        // Bob is dealt the Freeze and plays it elsewhere, so he is left holding nothing.
        var room = Started3(Freeze, Num(2), Num(3));

        await Assert.That(room.PendingChoice!.ChooserId).IsEqualTo(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.LineOf(Bob).IsEmpty).IsTrue();
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);

        var error = ExpectRuleError(() => room.Stay(Bob));
        await Assert.That(error.Message).Contains("card in front of you");
    }

    [Test]
    public async Task A_player_frozen_before_the_deal_reaches_them_gets_no_card()
    {
        // Deal order is Bob, Carol, Alice. Bob's Freeze lands on Carol before her card does.
        var room = Started3(Freeze, Num(2), Num(3));
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.NumbersOf(Carol)).IsEmpty();
        // Alice still gets hers, and it is the next card off the deck rather than Carol's.
        await Assert.That(room.NumbersOf(Alice)).IsEquivalentTo(new[] { 2 });
    }

    [Test]
    public async Task Round_totals_accumulate_across_rounds()
    {
        var room = Started3(Num(4), Num(2), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        room.NextRound(Alice);
        room.Stay(room.Round!.CurrentPlayerId!.Value);
        room.Stay(room.Round.CurrentPlayerId!.Value);
        room.Stay(room.Round.CurrentPlayerId!.Value);

        await Assert.That(room.Totals[Bob]).IsGreaterThan(0);
        await Assert.That(room.Round.RoundNumber).IsEqualTo(2);
    }
}
