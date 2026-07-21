using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class SecondChanceTests
{
    [Test]
    public async Task Held_until_a_duplicate_arrives()
    {
        var room = Started3(Num(1), Num(2), Num(3), SecondChance);
        room.Hit(Bob);

        await Assert.That(room.LineOf(Bob).HasSecondChance).IsTrue();
        await Assert.That(room.PendingChoice).IsNull();
    }

    [Test]
    public async Task Cancels_a_bust_spending_the_second_chance()
    {
        // The deck is one shared sequence, so the others stay rather than drawing Bob's cards.
        var room = Started3(Num(5), Num(2), Num(3), SecondChance, Num(5));
        room.Hit(Bob);   // Second Chance
        room.Stay(Carol);
        room.Stay(Alice);
        var discardBefore = room.DiscardCount;
        room.Hit(Bob);   // a second 5

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsFalse();   // no longer a live one
        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 5 });
        // Only the duplicate is discarded now; the spent Second Chance stays face up until the
        // round ends, so the table remembers it saved a bust.
        await Assert.That(room.DiscardCount).IsEqualTo(discardBefore + 1);
        await Assert.That(room.LineOf(Bob).Spent.Any(c => c is ActionCard { Kind: ActionKind.SecondChance })).IsTrue();
    }

    [Test]
    public async Task A_spent_second_chance_does_not_block_holding_another()
    {
        var room = Started3(Num(5), Num(2), Num(3), SecondChance, Num(5), SecondChance);
        room.Hit(Bob);   // Second Chance held
        room.Stay(Carol);
        room.Stay(Alice);
        room.Hit(Bob);   // duplicate 5 -> spends it
        room.Hit(Bob);   // a fresh Second Chance -> held again, the spent one doesn't count

        await Assert.That(room.LineOf(Bob).HasSecondChance).IsTrue();
        await Assert.That(room.LineOf(Bob).Spent.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Using_it_ends_the_turn_rather_than_dealing_again()
    {
        var room = Started3(Num(5), Num(2), Num(3), SecondChance, Num(9), Num(5));
        room.Hit(Bob);    // Second Chance
        room.Hit(Carol);  // 9
        room.Stay(Alice);
        room.Hit(Bob);    // a second 5 — spends the Second Chance

        // No replacement card now; he is dealt one on his next go.
        await Assert.That(room.NumbersOf(Bob)).IsEquivalentTo(new[] { 5 });
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Carol);
        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
    }

    [Test]
    public async Task A_second_one_must_be_given_away()
    {
        // Two eligible recipients, so there is a real decision to make.
        var room = Started3(Num(1), Num(2), Num(3), SecondChance, Num(9), Num(8), SecondChance);
        room.Hit(Bob);   // Bob holds one
        room.Hit(Carol);
        room.Hit(Alice);
        room.Hit(Bob);   // a second one

        await Assert.That(room.PendingChoice!.Kind).IsEqualTo(ChoiceKind.SecondChanceRecipient);
        await Assert.That(room.PendingChoice.ChooserId).IsEqualTo(Bob);

        room.ChooseTarget(Bob, Carol);
        await Assert.That(room.LineOf(Carol).HasSecondChance).IsTrue();
        await Assert.That(room.LineOf(Bob).HasSecondChance).IsTrue();
    }

    [Test]
    public async Task Cannot_keep_two()
    {
        var room = Started3(Num(1), Num(2), Num(3), SecondChance, Num(9), Num(8), SecondChance);
        room.Hit(Bob);
        room.Hit(Carol);
        room.Hit(Alice);
        room.Hit(Bob);

        // Giving it to yourself is not on the table.
        var error = ExpectRuleError(() => room.ChooseTarget(Bob, Bob));
        await Assert.That(error.Message).Contains("can't play that card on them");
    }

    [Test]
    public async Task Discarded_when_everyone_else_already_holds_one()
    {
        var room = Started3(
            SecondChance, SecondChance, SecondChance,  // one each on the deal
            SecondChance);                              // and a fourth for Bob
        var discardBefore = room.DiscardCount;
        room.Hit(Bob);

        // Nobody is eligible, so it just goes.
        await Assert.That(room.PendingChoice).IsNull();
        await Assert.That(room.DiscardCount).IsEqualTo(discardBefore + 1);
    }

    [Test]
    public async Task Discarded_when_there_are_no_other_active_players()
    {
        var room = Started3(Num(1), Num(2), Num(3), SecondChance, SecondChance);
        room.Hit(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        var discardBefore = room.DiscardCount;
        room.Hit(Bob);   // second one, and he is alone

        await Assert.That(room.PendingChoice).IsNull();
        await Assert.That(room.DiscardCount).IsEqualTo(discardBefore + 1);
    }

    [Test]
    public async Task Unused_ones_are_discarded_at_the_end_of_the_round()
    {
        var room = Started3(Num(1), Num(2), Num(3), SecondChance);
        room.Hit(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        room.Stay(Bob);

        await Assert.That(room.Phase).IsEqualTo(Flip7Phase.RoundResult);
        // Everything from the round is set aside; nothing is held over.
        await Assert.That(room.Round!.Hands.Values.All(h => h.Tableau.IsEmpty)).IsTrue();
    }

    [Test]
    public async Task Holding_one_is_worth_no_points()
    {
        var room = Started3(Num(4), Num(2), Num(3), SecondChance);
        room.Hit(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        room.Stay(Bob);

        await Assert.That(room.Totals[Bob]).IsEqualTo(4);
    }
}
