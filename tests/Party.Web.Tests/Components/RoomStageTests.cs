using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Party.Flip7;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The circuit plumbing every game's room sits on: joining, re-rendering when someone else
/// moves, surfacing a rejected move, and leaving. Nothing covered this before.
/// </summary>
public class RoomStageTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static RoomHandle<Flip7Room> Handle(params Card[] stack)
    {
        var deck = stack.ToList();
        deck.AddRange(Enumerable.Repeat(new NumberCard(0), 100));
        return new RoomHandle<Flip7Room>("TEST", GameType.Flip7, new Flip7Room("TEST", _ => deck, new Random(42)));
    }

    private static IRenderedComponent<TestStage> Render(BunitContext ctx, RoomHandle<Flip7Room> handle, Guid id, string name)
    {
        ctx.Services.AddSingleton(new RoomManager());   // the base injects it to close the room
        return ctx.Render<TestStage>(p => p
            .Add(x => x.Handle, handle)
            .Add(x => x.PlayerId, id)
            .Add(x => x.PlayerName, name));
    }

    [Test]
    public async Task Rendering_a_stage_puts_you_in_the_room()
    {
        using var ctx = new BunitContext();
        var handle = Handle();

        var stage = Render(ctx, handle, Alice, "Alice");

        await Assert.That(handle.Read(r => r.Players.Select(p => p.Name))).IsEquivalentTo(new[] { "Alice" });
        await Assert.That(handle.Read(r => r.Players[0].IsConnected)).IsTrue();
        await Assert.That(stage.Find(".phase").TextContent).IsEqualTo("Lobby");
    }

    [Test]
    public async Task A_move_that_works_leaves_no_banner()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        handle.Mutate(r =>
        {
            r.Join(Bob, "Bob");
            r.PlayerConnected(Bob);
            r.Join(Carol, "Carol");
            r.PlayerConnected(Carol);
        });

        await stage.InvokeAsync(() => stage.Instance.Do(r => r.StartGame(Alice)));

        await Assert.That(stage.FindAll(".banner.error")).IsEmpty();
        await Assert.That(stage.Find(".phase").TextContent).IsEqualTo("Turns");
    }

    [Test]
    public async Task A_rejected_move_re_renders_and_shows_why()
    {
        // The regression that matters. A rejection raises no Changed event, and the real Act is
        // called from a child panel's handler — so if Act doesn't re-render itself, the banner
        // never appears and the move just seems to do nothing.
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        var before = stage.Instance.Renders;

        await stage.InvokeAsync(() => stage.Instance.Do(r => r.StartGame(Alice)));   // only one player

        await Assert.That(stage.Instance.Renders).IsGreaterThan(before);
        await Assert.That(stage.Find(".banner.error").TextContent).Contains("at least 3 players");
    }

    [Test]
    public async Task The_banner_can_be_dismissed()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        await stage.InvokeAsync(() => stage.Instance.Do(r => r.StartGame(Alice)));
        await Assert.That(stage.FindAll(".banner.error")).Count().IsEqualTo(1);

        await stage.InvokeAsync(() =>
        {
            stage.Instance.Dismiss();
            stage.Render();
        });

        await Assert.That(stage.FindAll(".banner.error")).IsEmpty();
    }

    [Test]
    public async Task A_move_that_works_clears_an_earlier_complaint()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        await stage.InvokeAsync(() => stage.Instance.Do(r => r.StartGame(Alice)));
        await Assert.That(stage.FindAll(".banner.error")).Count().IsEqualTo(1);

        handle.Mutate(r =>
        {
            r.Join(Bob, "Bob");
            r.PlayerConnected(Bob);
            r.Join(Carol, "Carol");
            r.PlayerConnected(Carol);
        });
        await stage.InvokeAsync(() => stage.Instance.Do(r => r.StartGame(Alice)));

        await Assert.That(stage.FindAll(".banner.error")).IsEmpty();
    }

    [Test]
    public async Task Someone_elses_move_re_renders_this_circuit()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        var before = stage.Instance.Renders;

        // Another circuit joins; this one must notice without being touched.
        handle.Mutate(r =>
        {
            r.Join(Bob, "Bob");
            r.PlayerConnected(Bob);
        });

        await Assert.That(stage.Instance.Renders).IsGreaterThan(before);
    }

    [Test]
    public async Task Leaving_the_page_marks_the_circuit_gone()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        await Assert.That(handle.Read(r => r.Players[0].IsConnected)).IsTrue();

        stage.Instance.Dispose();

        await Assert.That(handle.Read(r => r.Players[0].IsConnected)).IsFalse();
    }

    [Test]
    public async Task A_disposed_stage_stops_listening()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        stage.Instance.Dispose();
        var before = stage.Instance.Renders;

        handle.Mutate(r => r.Join(Bob, "Bob"));

        await Assert.That(stage.Instance.Renders).IsEqualTo(before);
    }

    [Test]
    public async Task A_closed_room_says_so_instead_of_the_game()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");

        handle.Close();

        await Assert.That(stage.FindAll(".closed")).Count().IsEqualTo(1);
        await Assert.That(stage.FindAll(".phase")).IsEmpty();
    }

    [Test]
    public async Task The_host_can_close_the_room_for_everyone()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");   // Alice joins first, so she's host

        await stage.InvokeAsync(() => stage.Instance.Close());

        await Assert.That(handle.IsClosed).IsTrue();
        await Assert.That(handle.CloseReason).IsEqualTo(RoomCloseReason.HostClosed);
        await Assert.That(stage.Find(".closed").TextContent).Contains("host");
    }

    [Test]
    public async Task A_non_host_cannot_close_while_the_host_is_here()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        handle.Mutate(r =>
        {
            r.Join(Alice, "Alice");        // Alice is host, and present
            r.PlayerConnected(Alice);
            r.Join(Bob, "Bob");
            r.PlayerConnected(Bob);
        });
        var stage = Render(ctx, handle, Bob, "Bob");   // Bob is not the host

        await stage.InvokeAsync(() => stage.Instance.Close());

        await Assert.That(handle.IsClosed).IsFalse();
        await Assert.That(stage.FindAll(".closed")).IsEmpty();
    }

    [Test]
    public async Task Anyone_can_close_once_the_host_is_away()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        handle.Mutate(r =>
        {
            r.Join(Alice, "Alice");        // Alice is host
            r.PlayerConnected(Alice);
            r.Join(Bob, "Bob");
            r.PlayerConnected(Bob);
            r.PlayerDisconnected(Alice);   // ...but she's wandered off, so anyone may drive
        });
        var stage = Render(ctx, handle, Bob, "Bob");

        await stage.InvokeAsync(() => stage.Instance.Close());

        await Assert.That(handle.IsClosed).IsTrue();
    }

    [Test]
    public async Task Closing_removes_the_room_from_the_manager()
    {
        // Use a manager-created room so Rooms.Remove is actually exercised, not a no-op.
        using var ctx = new BunitContext();
        var rooms = new RoomManager();
        ctx.Services.AddSingleton(rooms);
        var handle = (RoomHandle<Flip7Room>)rooms.CreateRoom(GameType.Flip7);

        var stage = ctx.Render<TestStage>(p => p
            .Add(x => x.Handle, handle)
            .Add(x => x.PlayerId, Alice)
            .Add(x => x.PlayerName, "Alice"));   // Alice joins first, so she's host

        await stage.InvokeAsync(() => stage.Instance.Close());

        await Assert.That(handle.IsClosed).IsTrue();
        await Assert.That(rooms.TryGetRoom(handle.Code, out _)).IsFalse();
    }

    [Test]
    public async Task Being_turned_away_is_explained_rather_than_swallowed()
    {
        using var ctx = new BunitContext();
        var handle = Handle();
        handle.Mutate(r =>
        {
            for (var i = 0; i < Flip7Room.MaxPlayers; i++)
            {
                r.Join(Guid.NewGuid(), $"Player{i}");
            }
        });

        var stage = Render(ctx, handle, Alice, "Alice");

        await Assert.That(stage.Instance.Phase).IsNull();
        await Assert.That(stage.Find(".banner.error").TextContent).Contains("players max");
    }

    [Test]
    public async Task Disposing_a_stage_whose_room_already_closed_does_not_throw()
    {
        // The janitor can sweep the room between the check and the call.
        using var ctx = new BunitContext();
        var handle = Handle();
        var stage = Render(ctx, handle, Alice, "Alice");
        handle.Close();

        stage.Instance.Dispose();

        await Assert.That(handle.IsClosed).IsTrue();
    }
}
