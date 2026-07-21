using Bunit;
using Party.Flip7;
using Party.Web.Components.Game;

namespace Party.Web.Tests.Components;

/// <summary>Each card type carries its own colour class, so the table reads at a glance.</summary>
public class Flip7CardTests
{
    private static IReadOnlyList<string> Classes(BunitContext ctx, Card card) =>
        [.. ctx.Render<Flip7Card>(p => p.Add(x => x.Card, card)).Find(".f7card").ClassList];

    [Test]
    public async Task Numbers_and_modifiers_keep_their_classes()
    {
        using var ctx = new BunitContext();
        await Assert.That(Classes(ctx, new NumberCard(5))).Contains("num");
        await Assert.That(Classes(ctx, new ModifierCard(ModifierKind.Plus10))).Contains("mod");
        await Assert.That(Classes(ctx, new ModifierCard(ModifierKind.Times2))).Contains("mod");
    }

    [Test]
    public async Task Each_action_card_gets_a_distinct_colour_class()
    {
        using var ctx = new BunitContext();
        await Assert.That(Classes(ctx, new ActionCard(ActionKind.Freeze))).Contains("freeze");
        await Assert.That(Classes(ctx, new ActionCard(ActionKind.FlipThree))).Contains("flip3");
        await Assert.That(Classes(ctx, new ActionCard(ActionKind.SecondChance))).Contains("second");
    }

    [Test]
    public async Task A_faded_card_is_marked()
    {
        using var ctx = new BunitContext();
        var card = ctx.Render<Flip7Card>(p => p.Add(x => x.Card, new NumberCard(5)).Add(x => x.Faded, true));

        await Assert.That(card.Find(".f7card").ClassList).Contains("faded");
    }

    [Test]
    public async Task A_used_card_is_marked_used()
    {
        using var ctx = new BunitContext();
        var card = ctx.Render<Flip7Card>(p => p
            .Add(x => x.Card, new ActionCard(ActionKind.SecondChance))
            .Add(x => x.Used, true));

        await Assert.That(card.Find(".f7card").ClassList).Contains("used");
    }
}
