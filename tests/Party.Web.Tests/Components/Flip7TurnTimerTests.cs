using Bunit;
using Party.Flip7;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>The Flip 7 turn clock: shown while a turn is on the clock, and phrased for the viewer.</summary>
public class Flip7TurnTimerTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>A timed game where Bob is up first, with time still on his clock.</summary>
    private static Flip7Room TimedGame()
    {
        var deck = new List<Card> { new NumberCard(5), new NumberCard(4), new NumberCard(3) };
        deck.AddRange(Enumerable.Repeat<Card>(new NumberCard(0), 200));
        var room = new Flip7Room("TEST", _ => deck, new Random(42));   // real clock; deadline is 30s out
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        room.SetTurnTimer(Alice, 30);
        room.StartGame(Alice);
        return room;
    }

    private static IRenderedComponent<Flip7TurnTimer> Render(BunitContext ctx, Flip7Room room, Guid viewer) =>
        ctx.Render<Flip7TurnTimer>(p => p
            .Add(x => x.View, Flip7View.Build(room, viewer))
            .Add(x => x.Act, _ => { }));

    [Test]
    public async Task It_tells_the_player_on_the_clock_it_is_their_turn()
    {
        using var ctx = new BunitContext();
        var timer = Render(ctx, TimedGame(), Bob);   // Bob is up

        await Assert.That(timer.Find(".turn-timer").TextContent).Contains("your turn");
    }

    [Test]
    public async Task It_names_whose_turn_it_is_for_everyone_else()
    {
        using var ctx = new BunitContext();
        var timer = Render(ctx, TimedGame(), Carol);   // Carol is watching Bob's turn

        await Assert.That(timer.Find(".turn-timer").TextContent).Contains("Bob's turn");
    }

    [Test]
    public async Task It_shows_nothing_when_there_is_no_timer()
    {
        using var ctx = new BunitContext();
        var deck = new List<Card>();
        deck.AddRange(Enumerable.Repeat<Card>(new NumberCard(0), 200));
        var room = new Flip7Room("TEST", _ => deck, new Random(42));
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        room.StartGame(Alice);   // timer left off

        var timer = Render(ctx, room, Bob);

        await Assert.That(timer.FindAll(".turn-timer")).IsEmpty();
    }
}
