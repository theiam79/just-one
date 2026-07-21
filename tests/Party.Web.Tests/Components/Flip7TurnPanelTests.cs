using Bunit;
using Party.Flip7;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The turn panel is where a Flip 7 player is told what is being asked of them, and it says
/// four quite different things depending on state. Rendering it is the only way to know it says
/// the right one.
/// </summary>
public class Flip7TurnPanelTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static Flip7Room Room(params Card[] stack)
    {
        var deck = stack.ToList();
        deck.AddRange(Enumerable.Repeat(new NumberCard(0), 100));
        var room = new Flip7Room("TEST", _ => deck, new Random(42));
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        room.StartGame(Alice);
        return room;
    }

    private static IRenderedComponent<Flip7TurnPanel> Render(BunitContext ctx, Flip7Room room, Guid viewer) =>
        ctx.Render<Flip7TurnPanel>(p => p
            .Add(x => x.View, Flip7View.Build(room, viewer))
            .Add(x => x.Act, _ => { }));

    // ---- The prompt: which card am I placing? ----

    [Test]
    public async Task A_freeze_says_what_a_freeze_does()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);

        var text = Render(ctx, room, Bob).Markup;
        await Assert.That(text).Contains("Freeze someone");
        await Assert.That(text).DoesNotContain("Second Chance");
        await Assert.That(text).DoesNotContain("next three cards");
    }

    [Test]
    public async Task A_flip_three_says_what_a_flip_three_does()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.FlipThree));
        room.Hit(Bob);

        var text = Render(ctx, room, Bob).Markup;
        await Assert.That(text).Contains("next three cards");
        await Assert.That(text).DoesNotContain("Freeze someone");
    }

    [Test]
    public async Task A_surplus_second_chance_is_not_described_as_a_flip_three()
    {
        // The one the mutation matrix caught: the panel picks its wording from MyChoiceKind, and
        // the catch-all arm is the Flip Three text. Get this wrong and the prompt lies.
        using var ctx = new BunitContext();
        var room = Room(
            new NumberCard(1), new NumberCard(2), new NumberCard(3),
            new ActionCard(ActionKind.SecondChance), new NumberCard(9), new NumberCard(8),
            new ActionCard(ActionKind.SecondChance));
        room.Hit(Bob);
        room.Hit(Carol);
        room.Hit(Alice);
        room.Hit(Bob);

        var text = Render(ctx, room, Bob).Markup;
        await Assert.That(text).Contains("only hold one Second Chance");
        await Assert.That(text).DoesNotContain("next three cards");
        await Assert.That(text).DoesNotContain("Freeze someone");
    }

    [Test]
    public async Task The_chooser_gets_a_button_for_every_legal_target()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);

        var buttons = Render(ctx, room, Bob).FindAll(".f7targets .btn");
        await Assert.That(buttons.Count).IsEqualTo(3);
        await Assert.That(buttons.Select(b => b.TextContent.Trim())).IsEquivalentTo(new[] { "Alice", "Myself", "Carol" });
    }

    [Test]
    public async Task Choosing_a_target_plays_the_card_on_them()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);

        var panel = ctx.Render<Flip7TurnPanel>(p => p
            .Add(x => x.View, Flip7View.Build(room, Bob))
            .Add(x => x.Act, act => act(room)));

        panel.FindAll(".f7targets .btn").Single(b => b.TextContent.Trim() == "Carol").Click();

        await Assert.That(room.Round![Carol].Status).IsEqualTo(RoundStatus.Frozen);
    }

    [Test]
    public async Task Everyone_else_is_told_who_the_table_is_waiting_on()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);

        var panel = Render(ctx, room, Carol);
        // Onlookers are told who, and — since nothing here is hidden — what is being placed.
        await Assert.That(panel.Markup).Contains("Bob is placing a Freeze");
        await Assert.That(panel.FindAll(".f7played .f7card")).IsNotEmpty();
        // And they are not offered the choice themselves.
        await Assert.That(panel.FindAll(".f7targets .btn")).IsEmpty();
    }

    // ---- Hit and stay ----

    [Test]
    public async Task The_current_player_is_offered_both_moves()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3));

        var panel = Render(ctx, room, Bob);
        await Assert.That(panel.Markup).Contains("Your go");
        var buttons = panel.FindAll(".f7actions button");
        await Assert.That(buttons.Count).IsEqualTo(2);
        await Assert.That(buttons[0].TextContent.Trim()).IsEqualTo("Hit");
        await Assert.That(buttons[1].TextContent.Trim()).IsEqualTo("Stay on 5");
    }

    [Test]
    public async Task Stay_is_disabled_for_a_player_holding_nothing()
    {
        // The engine refuses it, so the button must not offer it.
        using var ctx = new BunitContext();
        var room = Room(new ActionCard(ActionKind.Freeze), new NumberCard(2), new NumberCard(3));
        room.ChooseTarget(Bob, Carol);   // Bob plays his only card away

        var panel = Render(ctx, room, Bob);
        var buttons = panel.FindAll(".f7actions button");
        await Assert.That(buttons[0].HasAttribute("disabled")).IsFalse();
        await Assert.That(buttons[1].HasAttribute("disabled")).IsTrue();
        await Assert.That(panel.Markup).Contains("nothing in front of you");
    }

    [Test]
    public async Task Hitting_takes_a_card()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3), new NumberCard(9));

        var panel = ctx.Render<Flip7TurnPanel>(p => p
            .Add(x => x.View, Flip7View.Build(room, Bob))
            .Add(x => x.Act, act => act(room)));
        panel.FindAll(".f7actions button")[0].Click();

        await Assert.That(room.Round![Bob].Tableau.Numbers.Select(n => n.Value)).IsEquivalentTo(new[] { 5, 9 });
    }

    [Test]
    public async Task Staying_banks_the_line()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3));

        var panel = ctx.Render<Flip7TurnPanel>(p => p
            .Add(x => x.View, Flip7View.Build(room, Bob))
            .Add(x => x.Act, act => act(room)));
        panel.FindAll(".f7actions button")[1].Click();

        await Assert.That(room.Round![Bob].Status).IsEqualTo(RoundStatus.Stayed);
    }

    // ---- Everyone else ----

    [Test]
    public async Task A_waiting_player_is_told_whose_go_it_is_and_offered_nothing()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3));

        var panel = Render(ctx, room, Carol);
        await Assert.That(panel.Markup).Contains("Bob's go");
        await Assert.That(panel.FindAll(".f7actions button")).IsEmpty();
    }

    [Test]
    public async Task A_watcher_is_told_they_are_watching()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3));
        var dave = Guid.NewGuid();
        room.Join(dave, "Dave");

        var panel = Render(ctx, room, dave);
        await Assert.That(panel.Markup).Contains("watching this one");
        await Assert.That(panel.FindAll(".f7actions button")).IsEmpty();
    }

    [Test]
    public async Task A_player_who_is_out_is_told_how_they_went_out()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3), new NumberCard(5));
        room.Hit(Bob);   // a second 5 — bust

        await Assert.That(Render(ctx, room, Bob).Markup).Contains("You busted");
    }

    [Test]
    public async Task A_player_who_stayed_is_reminded_what_they_kept()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3));
        room.Stay(Bob);

        await Assert.That(Render(ctx, room, Bob).Markup).Contains("You stayed on 5");
    }

    [Test]
    public async Task A_frozen_player_is_told_they_were_frozen()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(Render(ctx, room, Carol).Markup).Contains("You were frozen on 2");
    }
}
