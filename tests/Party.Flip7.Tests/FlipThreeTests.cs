using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class FlipThreeTests
{
    [Test]
    public async Task Deals_the_target_three_cards()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(4), Num(5), Num(6));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.NumbersOf(Carol)).IsEquivalentTo(new[] { 2, 4, 5, 6 });
    }

    [Test]
    public async Task The_card_itself_is_discarded_as_soon_as_it_resolves()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(4), Num(5), Num(6));
        room.Hit(Bob);
        var before = room.DiscardCount;
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.DiscardCount).IsEqualTo(before + 1);
    }

    [Test]
    public async Task You_may_flip_three_on_yourself()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(4), Num(5), Num(6));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Bob);

        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 1, 4, 5, 6 });
    }

    [Test]
    public async Task Stops_the_moment_the_target_busts()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(2), Num(5), Num(6));
        room.Hit(Bob);
        var deckBefore = room.DeckCount;
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Busted);
        // Only the busting card came off the deck; the other two were never flipped.
        await Assert.That(room.DeckCount).IsEqualTo(deckBefore - 1);
    }

    [Test]
    public async Task An_action_revealed_mid_flip_waits_until_all_three_are_down()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(4), Freeze, Num(5));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        // All three cards are down before the Freeze is even offered a target.
        await Assert.That(room.NumbersOf(Carol)).IsEquivalentTo(new[] { 2, 4, 5 });
        await Assert.That(room.PendingChoice!.Kind).IsEqualTo(ChoiceKind.ActionTarget);
        await Assert.That(room.PendingChoice.ChooserId).IsEqualTo(Carol);

        room.ChooseTarget(Carol, Alice);
        await Assert.That(room.StatusOf(Alice)).IsEqualTo(RoundStatus.Frozen);
    }

    [Test]
    public async Task A_set_aside_card_is_discarded_unresolved_if_the_flipper_busts()
    {
        // The Ed. 3.1 errata: the 2024 printing resolved these even on a bust.
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(4), Freeze, Num(2));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Busted);
        // The Freeze never gets a target — it just goes.
        await Assert.That(room.PendingChoice).IsNull();
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.StatusOf(Alice)).IsEqualTo(RoundStatus.Active);
    }

    [Test]
    public async Task A_second_chance_revealed_mid_flip_resolves_immediately_and_can_save_that_same_flip()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, SecondChance, Num(2), Num(5));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        // Kept on the spot rather than set aside, then spent on the duplicate two cards later.
        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.LineOf(Carol).HasSecondChance).IsFalse();
        await Assert.That(room.NumbersOf(Carol)).IsEquivalentTo(new[] { 2, 5 });
    }

    [Test]
    public async Task Set_aside_cards_are_assigned_before_any_of_them_takes_effect()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Freeze, Freeze, Num(4));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        // First Freeze assigned to Bob — but nothing fires yet, because the second is still
        // waiting for a target.
        room.ChooseTarget(Carol, Bob);
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.PendingChoice).IsNotNull();

        room.ChooseTarget(Carol, Alice);
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(room.StatusOf(Alice)).IsEqualTo(RoundStatus.Frozen);
    }

    [Test]
    public async Task Set_aside_cards_resolve_in_the_order_they_were_flipped()
    {
        // A Freeze then a Flip Three, both aimed at Alice: the Freeze lands first, so the
        // Flip Three finds her already out and is simply discarded.
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Freeze, FlipThree, Num(4), Num(6));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        room.ChooseTarget(Carol, Alice);  // the Freeze
        room.ChooseTarget(Carol, Alice);  // the Flip Three

        await Assert.That(room.StatusOf(Alice)).IsEqualTo(RoundStatus.Frozen);
        // She was frozen before it could fire, so she never flipped anything.
        await Assert.That(room.NumbersOf(Alice)).IsEquivalentTo(new[] { 3 });
    }

    [Test]
    public async Task A_flip_three_can_chain_into_another()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree, Num(4), FlipThree, Num(5), Num(6), Num(7), Num(8));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);     // Carol flips 4, [FlipThree set aside], 5
        room.ChooseTarget(Carol, Alice);   // and hands the second Flip Three to Alice

        await Assert.That(room.NumbersOf(Carol)).IsEquivalentTo(new[] { 2, 4, 5 });
        await Assert.That(room.NumbersOf(Alice)).IsEquivalentTo(new[] { 3, 6, 7, 8 });
    }

    [Test]
    public async Task A_set_aside_card_is_discarded_unresolved_if_the_flipper_hits_flip7()
    {
        // The spec names bust *and* Flip 7 as stop conditions, and both drop the set-aside
        // cards. Carol has six numbers when Alice's Flip Three lands on her: the first flip
        // makes seven, so the Freeze behind it never gets a target.
        var room = Started3(
            Num(1), Num(2), Num(3),
            Num(4), Num(9),
            Num(5), Num(10),
            Num(6), Num(11),
            Num(7), Num(12),
            Num(8), Num(0),
            FlipThree,        // Carol turns it up with six numbers down
            Num(9), Freeze, Num(6));

        room.Stay(Bob);
        for (var i = 0; i < 5; i++)
        {
            room.Hit(Carol);
            room.Hit(Alice);
        }

        room.Hit(Carol);                  // the Flip Three
        room.ChooseTarget(Carol, Carol);  // played on herself; the first flip is her seventh

        await Assert.That(room.Round!.Flip7PlayerId).IsEqualTo(Carol);
        await Assert.That(room.PendingChoice).IsNull();
        await Assert.That(room.StatusOf(Alice)).IsNotEqualTo(RoundStatus.Frozen);
        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
    }
}
