using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Party.Web.Components.Pages;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The front door: pick a game, or type a code and let the room decide which game you're in.
/// </summary>
public class HomeTests
{
    private static BunitContext NewContext(RoomManager? rooms = null)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(rooms ?? new RoomManager());
        // The page remembers your name in localStorage; none of that matters here.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string>("party.getName").SetResult("");
        return ctx;
    }

    private static IElement Choice(IRenderedComponent<Home> home, string game) =>
        home.FindAll(".game-choice").Single(c => c.QuerySelector(".game-name")!.TextContent == game);

    [Test]
    public async Task Every_game_is_on_offer()
    {
        using var ctx = NewContext();
        var home = ctx.Render<Home>();

        var names = home.FindAll(".game-name").Select(e => e.TextContent);
        await Assert.That(names).IsEquivalentTo(GameInfo.All.Select(g => g.Name));
    }

    [Test]
    public async Task Each_game_says_what_it_is()
    {
        using var ctx = NewContext();
        var home = ctx.Render<Home>();

        foreach (var game in GameInfo.All)
        {
            var card = Choice(home, game.Name);
            await Assert.That(card.TextContent).Contains(game.Tagline);
            await Assert.That(card.TextContent).Contains(game.Blurb);
        }
    }

    [Test]
    public async Task Picking_a_game_creates_a_room_of_that_game()
    {
        var rooms = new RoomManager();
        using var ctx = NewContext(rooms);
        var home = ctx.Render<Home>();
        home.Find("#name").Input("Alice");

        Choice(home, "Flip 7").Click();

        await Assert.That(rooms.Rooms).Count().IsEqualTo(1);
        await Assert.That(rooms.Rooms.Single().Game).IsEqualTo(GameType.Flip7);
        await Assert.That(ctx.Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().Uri)
            .Contains($"/room/{rooms.Rooms.Single().Code}");
    }

    [Test]
    public async Task Picking_the_other_game_creates_that_one()
    {
        var rooms = new RoomManager();
        using var ctx = NewContext(rooms);
        var home = ctx.Render<Home>();
        home.Find("#name").Input("Alice");

        Choice(home, "Just One").Click();

        await Assert.That(rooms.Rooms.Single().Game).IsEqualTo(GameType.JustOne);
    }

    [Test]
    public async Task A_nameless_player_is_asked_for_one_and_no_room_is_made()
    {
        var rooms = new RoomManager();
        using var ctx = NewContext(rooms);
        var home = ctx.Render<Home>();

        Choice(home, "Flip 7").Click();

        await Assert.That(home.Find(".banner.error").TextContent).Contains("Enter a name");
        await Assert.That(rooms.Rooms).IsEmpty();
    }

    [Test]
    public async Task Joining_by_code_never_asks_which_game()
    {
        // The point of one code space: the code already knows.
        var rooms = new RoomManager();
        var room = rooms.CreateRoom(GameType.Flip7);
        using var ctx = NewContext(rooms);
        var home = ctx.Render<Home>();
        home.Find("#name").Input("Bob");
        home.Find(".code-input").Input(room.Code);

        home.Find(".join-row").Submit();

        await Assert.That(ctx.Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().Uri)
            .Contains($"/room/{room.Code}");
    }

    [Test]
    public async Task A_code_is_matched_however_it_is_typed()
    {
        var rooms = new RoomManager();
        var room = rooms.CreateRoom(GameType.JustOne);
        using var ctx = NewContext(rooms);
        var home = ctx.Render<Home>();
        home.Find("#name").Input("Bob");
        home.Find(".code-input").Input(room.Code.ToLowerInvariant());

        home.Find(".join-row").Submit();

        await Assert.That(ctx.Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().Uri)
            .Contains($"/room/{room.Code}");
    }

    [Test]
    public async Task An_unknown_code_says_so_and_goes_nowhere()
    {
        using var ctx = NewContext();
        var home = ctx.Render<Home>();
        home.Find("#name").Input("Bob");
        home.Find(".code-input").Input("ZZZZ");

        home.Find(".join-row").Submit();

        await Assert.That(home.Find(".banner.error").TextContent).Contains("No room with that code");
        await Assert.That(ctx.Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().Uri)
            .DoesNotContain("/room/");
    }

    [Test]
    public async Task The_name_is_remembered_for_next_time()
    {
        using var ctx = NewContext();
        var home = ctx.Render<Home>();
        home.Find("#name").Input("  Alice  ");

        Choice(home, "Flip 7").Click();

        // Trimmed on the way out, so it comes back clean next visit.
        var saved = ctx.JSInterop.Invocations["party.setName"].Single();
        await Assert.That(saved.Arguments.Single()).IsEqualTo("Alice");
    }
}
