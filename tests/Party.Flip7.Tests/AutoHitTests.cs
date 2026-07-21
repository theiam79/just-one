using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

/// <summary>
/// The opt-in "keep hitting while a Second Chance covers me" preference: safe because a bust just
/// spends the Second Chance, and it stops the moment that cover is gone.
/// </summary>
public class AutoHitTests
{
    [Test]
    public async Task It_is_off_by_default_so_a_held_second_chance_just_waits()
    {
        var room = Started3(Num(5), Num(1), Num(2), SecondChance);
        room.Hit(Bob);       // Bob holds a Second Chance; turn -> Carol
        room.Stay(Carol);
        room.Stay(Alice);    // only Bob left — his turn, but nothing is taken for him

        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsTrue();
        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 5 });
    }

    [Test]
    public async Task With_no_second_chance_the_option_still_waits()
    {
        var room = Started3(Num(5), Num(1), Num(2));
        room.SetAutoHitSecondChance(Bob, true);   // on, but Bob holds no Second Chance

        // Bob is up first and uncovered, so his turn waits for a real decision.
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 5 });
    }

    [Test]
    public async Task While_covered_it_takes_cards_and_stops_once_the_cover_is_spent()
    {
        var room = Started3(Num(5), Num(1), Num(2), SecondChance, Num(5));
        room.Hit(Bob);                            // Bob holds a Second Chance
        room.SetAutoHitSecondChance(Bob, true);
        room.Stay(Carol);
        room.Stay(Alice);                         // turn returns to Bob → auto-hit draws the second 5

        // The duplicate spent the Second Chance; Bob survives, and with the cover gone the run stops
        // and hands the decision back.
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsFalse();
        await Assert.That(room.LineOf(Bob).Spent.Count).IsEqualTo(1);
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
    }

    [Test]
    public async Task Turning_it_off_mid_run_stops_the_auto_hits()
    {
        var room = Started3(Num(5), Num(1), Num(2), SecondChance);
        room.Hit(Bob);                            // Bob holds a Second Chance
        room.SetAutoHitSecondChance(Bob, true);
        room.SetAutoHitSecondChance(Bob, false);  // changed their mind before it mattered
        room.Stay(Carol);
        room.Stay(Alice);

        // Off again, so Bob's turn waits even though he's still covered.
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsTrue();
    }

    [Test]
    public async Task A_safe_run_keeps_going_until_the_cover_is_spent()
    {
        var room = Started3(
            Num(5), Num(1), Num(2),   // deal
            SecondChance,             // Bob holds it
            Num(6), Num(7), Num(5));  // auto-hits: 6, 7, then a duplicate 5 spends the cover
        room.Hit(Bob);
        room.SetAutoHitSecondChance(Bob, true);
        room.Stay(Carol);
        room.Stay(Alice);            // Bob is the only one left: 6, 7 taken, then 5 spends the cover

        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 5, 6, 7 });
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsFalse();
        await Assert.That(room.LineOf(Bob).Spent.Count).IsEqualTo(1);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);   // now waiting
    }

    [Test]
    public async Task An_auto_drawn_action_card_still_waits_for_a_human_to_place_it()
    {
        // Two players are still in, so a Freeze Bob auto-draws needs a target — the run must pause
        // for him to choose rather than auto-place it.
        var room = Started3(
            Num(5), Num(1), Num(2),   // deal
            SecondChance,             // Bob holds it
            Num(6),                   // Carol takes a card, stays active
            Freeze);                  // Bob auto-draws this
        room.Hit(Bob);
        room.SetAutoHitSecondChance(Bob, true);
        room.Hit(Carol);             // Carol active with 1, 6
        room.Stay(Alice);            // turn -> Bob; auto-hit draws the Freeze -> needs a target

        await Assert.That(room.PendingChoice).IsNotNull();
        await Assert.That(room.PendingChoice!.ChooserId).IsEqualTo(Bob);
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);   // not auto-frozen
    }

    [Test]
    public async Task Someone_not_in_the_room_cannot_set_it()
    {
        var room = Started3(Num(5), Num(1), Num(2));

        ExpectRuleError(() => room.SetAutoHitSecondChance(Dave, true));
    }
}
