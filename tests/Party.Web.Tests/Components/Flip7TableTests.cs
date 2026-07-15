using AngleSharp.Dom;
using Bunit;
using Party.Flip7;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The table is the game's primary surface: everyone's cards, face up, and how each player is
/// doing. Flip 7 hides nothing, so what it draws is simply the truth of the round.
/// </summary>
public class Flip7TableTests
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

    private static IRenderedComponent<Flip7Table> Render(BunitContext ctx, Flip7Room room, Guid viewer) =>
        ctx.Render<Flip7Table>(p => p.Add(x => x.View, Flip7View.Build(room, viewer)));

    private static IElement Seat(IRenderedComponent<Flip7Table> table, string name) =>
        table.FindAll(".f7seat").Single(s => s.QuerySelector(".player-name")!.TextContent == name);

    [Test]
    public async Task Every_player_gets_a_seat()
    {
        using var ctx = new BunitContext();
        var table = Render(ctx, Room(new NumberCard(1), new NumberCard(2), new NumberCard(3)), Alice);

        await Assert.That(table.FindAll(".f7seat")).Count().IsEqualTo(3);
    }

    [Test]
    public async Task A_watcher_gets_no_seat()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        room.Join(Guid.NewGuid(), "Dave");   // mid-game

        await Assert.That(Render(ctx, room, Alice).FindAll(".f7seat")).Count().IsEqualTo(3);
    }

    [Test]
    public async Task Cards_are_face_up_for_everyone()
    {
        // Flip 7 hides nothing, so Bob's line is as visible to Carol as it is to Bob.
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(7), new NumberCard(2), new NumberCard(3));

        foreach (var viewer in new[] { Alice, Bob, Carol })
        {
            var line = Seat(Render(ctx, room, viewer), "Bob").QuerySelectorAll(".f7card");
            await Assert.That(line.Select(c => c.TextContent)).IsEquivalentTo(new[] { "7" });
        }
    }

    [Test]
    public async Task Card_faces_are_drawn_as_they_read()
    {
        using var ctx = new BunitContext();
        var room = Room(
            new NumberCard(7), new NumberCard(2), new NumberCard(3),
            new ModifierCard(ModifierKind.Times2));
        room.Hit(Bob);

        var cards = Seat(Render(ctx, room, Alice), "Bob").QuerySelectorAll(".f7card");
        await Assert.That(cards.Select(c => c.TextContent)).IsEquivalentTo(new[] { "7", "×2" });
        await Assert.That(cards.Last().ClassList).Contains("mod");
    }

    [Test]
    public async Task A_plus_modifier_shows_its_value()
    {
        using var ctx = new BunitContext();
        var room = Room(
            new NumberCard(7), new NumberCard(2), new NumberCard(3),
            new ModifierCard(ModifierKind.Plus10));
        room.Hit(Bob);

        var cards = Seat(Render(ctx, room, Alice), "Bob").QuerySelectorAll(".f7card");
        await Assert.That(cards.Select(c => c.TextContent)).Contains("+10");
    }

    [Test]
    public async Task An_empty_line_says_so()
    {
        using var ctx = new BunitContext();
        var room = Room(new ActionCard(ActionKind.Freeze), new NumberCard(2), new NumberCard(3));
        room.ChooseTarget(Bob, Carol);   // Bob plays his only card away

        await Assert.That(Seat(Render(ctx, room, Alice), "Bob").TextContent).Contains("nothing yet");
    }

    [Test]
    public async Task Whose_go_it_is_is_marked()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var table = Render(ctx, room, Alice);

        await Assert.That(Seat(table, "Bob").TextContent).Contains("their go");
        await Assert.That(Seat(table, "Bob").ClassList).Contains("turn");
        await Assert.That(Seat(table, "Carol").TextContent).DoesNotContain("their go");
    }

    [Test]
    public async Task The_dealer_is_marked()
    {
        using var ctx = new BunitContext();
        var table = Render(ctx, Room(new NumberCard(1), new NumberCard(2), new NumberCard(3)), Alice);

        await Assert.That(Seat(table, "Alice").TextContent).Contains("deals");
        await Assert.That(Seat(table, "Bob").TextContent).DoesNotContain("deals");
    }

    [Test]
    public async Task A_bust_is_shown_and_the_line_is_dimmed_and_worth_nothing()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3), new NumberCard(5));
        room.Hit(Bob);

        var seat = Seat(Render(ctx, room, Alice), "Bob");
        await Assert.That(seat.TextContent).Contains("bust");
        await Assert.That(seat.TextContent).Contains("0 this round");
        await Assert.That(seat.ClassList).Contains("busted");
        await Assert.That(seat.QuerySelectorAll(".f7card.faded")).IsNotEmpty();
    }

    [Test]
    public async Task A_frozen_player_is_shown_frozen_with_the_card_that_did_it()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        var seat = Seat(Render(ctx, room, Alice), "Carol");
        await Assert.That(seat.TextContent).Contains("frozen");
        // The Freeze stays in front of them until the round ends.
        await Assert.That(seat.QuerySelectorAll(".f7card").Select(c => c.TextContent)).Contains("❄");
    }

    [Test]
    public async Task A_stayed_player_is_shown_with_what_they_kept()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(9), new NumberCard(2), new NumberCard(3));
        room.Stay(Bob);

        var seat = Seat(Render(ctx, room, Alice), "Bob");
        await Assert.That(seat.TextContent).Contains("stayed");
        await Assert.That(seat.TextContent).Contains("9 this round");
    }

    [Test]
    public async Task A_frozen_player_still_banks_their_modifiers()
    {
        // The whole point of Freeze versus bust, shown on the table.
        using var ctx = new BunitContext();
        var room = Room(
            new NumberCard(5), new NumberCard(2), new NumberCard(3),
            new ModifierCard(ModifierKind.Plus10), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);        // Bob: 5, +10
        room.Hit(Carol);
        room.ChooseTarget(Carol, Bob);

        await Assert.That(Seat(Render(ctx, room, Alice), "Bob").TextContent).Contains("15 this round");
    }

    [Test]
    public async Task Your_own_seat_is_marked_as_yours()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));

        await Assert.That(Seat(Render(ctx, room, Carol), "Carol").ClassList).Contains("me");
        await Assert.That(Seat(Render(ctx, room, Carol), "Bob").ClassList).DoesNotContain("me");
    }

    [Test]
    public async Task An_away_player_is_shown_away()
    {
        using var ctx = new BunitContext();
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        room.PlayerDisconnected(Carol);

        var seat = Seat(Render(ctx, room, Alice), "Carol");
        await Assert.That(seat.ClassList).Contains("away");
        await Assert.That(seat.QuerySelector(".dot")!.ClassList).Contains("off");
    }
}
