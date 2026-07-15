using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class FreezeTests
{
    [Test]
    public async Task Freeze_takes_the_target_out_of_the_round()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Alice);
    }

    [Test]
    public async Task A_frozen_player_still_banks_their_modifiers()
    {
        // The whole point of Freeze vs bust: frozen keeps the +10, busted loses it.
        var room = Started3(Num(5), Num(2), Num(3), Mod(ModifierKind.Plus10), Freeze);
        room.Hit(Bob);  // Bob: 5, +10
        room.Stay(Carol);
        room.Stay(Alice);
        // Bob is the only one left; his Freeze must land on himself.
        room.Hit(Bob);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.Totals[Bob]).IsEqualTo(15);
    }

    [Test]
    public async Task You_may_freeze_yourself()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);
        room.ChooseTarget(Bob, Bob);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.Totals.ContainsKey(Bob)).IsTrue();
    }

    [Test]
    public async Task The_only_active_player_must_freeze_themselves()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Stay(Bob);
        room.Stay(Carol);
        // Alice is the last one standing, so there is nothing to ask.
        room.Hit(Alice);

        await Assert.That(room.PendingChoice).IsNull();
        await Assert.That(room.StatusOf(Alice)).IsEqualTo(RoundStatus.Frozen);
    }

    [Test]
    public async Task Second_chance_does_not_block_a_freeze()
    {
        var room = Started3(Num(1), Num(2), Num(3), SecondChance, Freeze);
        room.Hit(Bob);                  // Bob holds a Second Chance
        room.Hit(Carol);                // Carol turns up a Freeze
        room.ChooseTarget(Carol, Bob);  // and puts it on Bob

        // The Second Chance is no help here — it only ever cancels a bust — and it stays in
        // front of him unspent.
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsTrue();
    }

    [Test]
    public async Task The_freeze_card_stays_in_front_of_the_frozen_player()
    {
        // It is not discarded on resolution, so a mid-round reshuffle cannot pick it up.
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);
        var discardBefore = room.DiscardCount;
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.LineOf(Carol).Cards.Any(c => c is ActionCard { Kind: ActionKind.Freeze })).IsTrue();
        await Assert.That(room.DiscardCount).IsEqualTo(discardBefore);
    }

    [Test]
    public async Task A_frozen_player_is_not_offered_a_turn()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);
        room.Stay(Alice);

        // Only Bob is left; Carol is never asked again.
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
    }

    [Test]
    public async Task Freezing_the_last_active_player_ends_the_round()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Stay(Bob);
        room.Stay(Carol);
        room.Hit(Alice); // self-freeze, nobody left

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }

    [Test]
    public async Task Cannot_freeze_a_player_who_is_already_out()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Stay(Bob);
        room.Hit(Carol);

        var error = ExpectRuleError(() => room.ChooseTarget(Carol, Bob));
        await Assert.That(error.Message).Contains("can't play that card on them");
    }

    [Test]
    public async Task Someone_else_cannot_answer_your_choice()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);

        var error = ExpectRuleError(() => room.ChooseTarget(Carol, Alice));
        await Assert.That(error.Message).Contains("not your card");
    }

    [Test]
    public async Task No_hit_or_stay_while_a_card_needs_a_target()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);

        var error = ExpectRuleError(() => room.Stay(Bob));
        await Assert.That(error.Message).Contains("card to play first");
    }
}
