using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The one roster both games share. Forking it instead of slotting it is what once left the host
/// unable to sit anyone out mid-game and made a mid-game joiner invisible to everybody.
/// </summary>
public class PlayerListTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static RosterEntry Seat(
        Guid id,
        string name,
        bool host = false,
        bool connected = true,
        bool spectator = false,
        bool benched = false) => new(id, name, host, connected, spectator, benched);

    private static IRenderedComponent<PlayerList> Render(
        BunitContext ctx,
        IReadOnlyList<RosterEntry> seats,
        Guid me,
        bool hostPowers = false,
        EventCallback<Guid>? onBench = null,
        RenderFragment<RosterEntry>? badges = null)
    {
        return ctx.Render<PlayerList>(p =>
        {
            p.Add(x => x.Seats, seats);
            p.Add(x => x.MyId, me);
            p.Add(x => x.HasHostPowers, hostPowers);
            if (onBench is { } cb)
            {
                p.Add(x => x.OnBench, cb);
            }

            if (badges is not null)
            {
                p.Add(x => x.Badges, badges);
            }
        });
    }

    private static IElement Row(IRenderedComponent<PlayerList> list, string name) =>
        list.FindAll(".player").Single(e => e.QuerySelector(".player-name")!.TextContent == name);

    [Test]
    public async Task Everyone_gets_a_row()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice", host: true), Seat(Bob, "Bob")], Alice);

        await Assert.That(list.FindAll(".player")).Count().IsEqualTo(2);
        await Assert.That(Row(list, "Alice").TextContent).Contains("★");
        await Assert.That(Row(list, "Bob").TextContent).DoesNotContain("★");
    }

    [Test]
    public async Task A_watcher_is_told_they_are_watching()
    {
        // The bug: a mid-game joiner appeared nowhere at all.
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice"), Seat(Bob, "Bob", spectator: true)], Alice);

        await Assert.That(Row(list, "Bob").TextContent).Contains("watching");
    }

    [Test]
    public async Task Someone_sat_out_says_so_rather_than_watching()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice"), Seat(Bob, "Bob", connected: false, spectator: true, benched: true)], Alice);

        await Assert.That(Row(list, "Bob").TextContent).Contains("sitting out");
        await Assert.That(Row(list, "Bob").TextContent).DoesNotContain("watching");
    }

    [Test]
    public async Task Away_players_are_shown_away()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice"), Seat(Bob, "Bob", connected: false)], Alice);

        await Assert.That(Row(list, "Bob").ClassList).Contains("away");
        await Assert.That(Row(list, "Bob").QuerySelector(".dot")!.ClassList).Contains("off");
        await Assert.That(Row(list, "Alice").QuerySelector(".dot")!.ClassList).Contains("on");
    }

    [Test]
    public async Task Your_own_row_is_marked()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice"), Seat(Bob, "Bob")], Bob);

        await Assert.That(Row(list, "Bob").ClassList).Contains("me");
        await Assert.That(Row(list, "Alice").ClassList).DoesNotContain("me");
    }

    // ---- The host's one roster power ----

    [Test]
    public async Task The_host_can_sit_out_an_away_player()
    {
        // The bug this fixes: this control only existed in the lobby, where it is no use.
        using var ctx = new BunitContext();
        Guid? benched = null;
        var list = Render(ctx, [Seat(Alice, "Alice", host: true), Seat(Bob, "Bob", connected: false)], Alice,
            hostPowers: true,
            onBench: EventCallback.Factory.Create<Guid>(new object(), id => benched = id));

        Row(list, "Bob").QuerySelector("button")!.Click();

        await Assert.That(benched).IsEqualTo(Bob);
    }

    [Test]
    public async Task There_is_nothing_to_offer_for_someone_who_is_here()
    {
        // The engine refuses to sit out a connected player, so don't offer it.
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice", host: true), Seat(Bob, "Bob")], Alice, hostPowers: true);

        await Assert.That(Row(list, "Bob").QuerySelectorAll("button")).IsEmpty();
    }

    [Test]
    public async Task Only_the_host_is_offered_it()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice", host: true), Seat(Bob, "Bob", connected: false)], Bob);

        await Assert.That(list.FindAll("button")).IsEmpty();
    }

    [Test]
    public async Task You_cannot_sit_yourself_out()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice", host: true, connected: false)], Alice, hostPowers: true);

        await Assert.That(list.FindAll("button")).IsEmpty();
    }

    [Test]
    public async Task Someone_already_sitting_out_is_not_offered_it_again()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice", host: true), Seat(Bob, "Bob", connected: false, spectator: true, benched: true)],
            Alice, hostPowers: true);

        await Assert.That(Row(list, "Bob").QuerySelectorAll("button")).IsEmpty();
    }

    // ---- The slot ----

    [Test]
    public async Task A_game_can_say_whatever_it_likes_against_a_name()
    {
        // The slot is the whole point: this component knows about neither game.
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice"), Seat(Bob, "Bob")], Alice,
            badges: seat => builder => builder.AddMarkupContent(0, $"<span class=\"mine\">{seat.Name.Length}</span>"));

        await Assert.That(Row(list, "Alice").QuerySelector(".mine")!.TextContent).IsEqualTo("5");
        await Assert.That(Row(list, "Bob").QuerySelector(".mine")!.TextContent).IsEqualTo("3");
    }

    [Test]
    public async Task Without_a_slot_it_still_renders()
    {
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice")], Alice);

        await Assert.That(list.FindAll(".player")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Your_own_row_carries_the_you_marker()
    {
        // The marker lives on the shared roster, so it must work here — the surface Just One uses.
        using var ctx = new BunitContext();
        var list = Render(ctx, [Seat(Alice, "Alice"), Seat(Bob, "Bob")], Bob);

        await Assert.That(Row(list, "Bob").QuerySelector(".you")).IsNotNull();
        await Assert.That(Row(list, "Alice").QuerySelector(".you")).IsNull();
    }
}
