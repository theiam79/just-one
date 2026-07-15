namespace Party.Flip7.Tests;

public class DeckTests
{
    [Test]
    public async Task Deck_has_ninety_four_cards()
    {
        await Assert.That(Flip7Deck.Single().Count).IsEqualTo(94);
    }

    [Test]
    public async Task Seventy_nine_number_cards()
    {
        await Assert.That(Flip7Deck.Single().OfType<NumberCard>().Count()).IsEqualTo(79);
    }

    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(2, 2)]
    [Arguments(3, 3)]
    [Arguments(4, 4)]
    [Arguments(5, 5)]
    [Arguments(6, 6)]
    [Arguments(7, 7)]
    [Arguments(8, 8)]
    [Arguments(9, 9)]
    [Arguments(10, 10)]
    [Arguments(11, 11)]
    [Arguments(12, 12)]
    public async Task Number_counts_equal_face_value_except_zero(int value, int expected)
    {
        var count = Flip7Deck.Single().OfType<NumberCard>().Count(n => n.Value == value);
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Six_modifiers_one_of_each()
    {
        var modifiers = Flip7Deck.Single().OfType<ModifierCard>().ToList();
        await Assert.That(modifiers.Count).IsEqualTo(6);
        await Assert.That(modifiers.Select(m => m.Kind).Distinct().Count()).IsEqualTo(6);
    }

    [Test]
    [Arguments(ActionKind.Freeze)]
    [Arguments(ActionKind.FlipThree)]
    [Arguments(ActionKind.SecondChance)]
    public async Task Three_of_each_action(ActionKind kind)
    {
        var count = Flip7Deck.Single().OfType<ActionCard>().Count(a => a.Kind == kind);
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    [Arguments(3, 1)]
    [Arguments(18, 1)]
    [Arguments(19, 2)]
    [Arguments(20, 2)]
    public async Task Decks_scale_with_players(int players, int decks)
    {
        await Assert.That(Flip7Deck.DecksFor(players)).IsEqualTo(decks);
    }

    [Test]
    public async Task Two_decks_is_twice_the_cards()
    {
        await Assert.That(Flip7Deck.Copies(2).Count).IsEqualTo(188);
    }

    [Test]
    public async Task Shuffling_keeps_every_card()
    {
        var shuffled = Flip7Deck.Shuffled(new Random(7))(3);
        await Assert.That(shuffled.Count).IsEqualTo(94);
        await Assert.That(shuffled.OrderBy(c => c.ToString()).SequenceEqual(
            Flip7Deck.Single().OrderBy(c => c.ToString()))).IsTrue();
    }
}
