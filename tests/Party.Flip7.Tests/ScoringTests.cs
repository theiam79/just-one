using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class ScoringTests
{
    private static Tableau Line(params Card[] cards)
    {
        var tableau = new Tableau();
        foreach (var card in cards)
        {
            tableau.Add(card);
        }

        return tableau;
    }

    // The worked examples printed in the rulebook.

    [Test]
    public async Task Sums_number_cards()
    {
        var line = Line(Num(3), Num(11), Num(5), Num(7), Num(10));
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(36);
    }

    [Test]
    public async Task Times2_doubles_the_number_cards()
    {
        var line = Line(Num(3), Num(11), Num(5), Num(7), Num(10), Times2);
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(72);
    }

    [Test]
    public async Task Plus_modifier_adds_after_the_numbers()
    {
        var line = Line(Num(3), Num(11), Num(5), Num(7), Num(10), Mod(ModifierKind.Plus10));
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(46);
    }

    [Test]
    public async Task Seven_numbers_scores_the_bonus()
    {
        var line = Line(Num(3), Num(11), Num(5), Num(7), Num(10), Num(9), Num(4));
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(64);
    }

    [Test]
    public async Task Times2_multiplies_the_numbers_but_not_the_modifiers()
    {
        // 36 x2 = 72, then +10 — not (36 + 10) x2.
        var line = Line(Num(3), Num(11), Num(5), Num(7), Num(10), Times2, Mod(ModifierKind.Plus10));
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(82);
    }

    [Test]
    public async Task Times2_does_not_multiply_the_flip7_bonus()
    {
        // 49 x2 = 98, then +15 — not (49 + 15) x2.
        var line = Line(Num(3), Num(11), Num(5), Num(7), Num(10), Num(9), Num(4), Times2);
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(113);
    }

    [Test]
    public async Task Modifiers_without_numbers_score_nothing_through_times2()
    {
        // 2 x 0 = 0, then the plus modifiers land on top.
        var line = Line(Mod(ModifierKind.Plus2), Mod(ModifierKind.Plus6), Times2);
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(8);
    }

    [Test]
    public async Task Two_times2_cards_stack_to_four()
    {
        // Only reachable on a table big enough for a second deck; the published rules never
        // contemplate it, so we reward it.
        var line = Line(Num(10), Num(8), Times2, Times2);
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(72);
    }

    [Test]
    public async Task Busting_scores_nothing()
    {
        var line = Line(Num(12), Num(11), Mod(ModifierKind.Plus10), Times2);
        await Assert.That(Flip7Rules.Score(line, busted: true)).IsEqualTo(0);
    }

    [Test]
    public async Task Zero_counts_as_a_number_card_worth_nothing()
    {
        var line = Line(Num(0), Num(5));
        await Assert.That(Flip7Rules.Score(line, busted: false)).IsEqualTo(5);
        await Assert.That(line.NumberCount).IsEqualTo(2);
    }
}
