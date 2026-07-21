using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Party.Flip7;
using Party.JustOne;
using Party.Web.Components.Pages;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The page follows a room across a game switch: when the handle behind its code is superseded,
/// it re-opens on the new game, carrying the player's connection from the old stage to the new.
/// </summary>
public class RoomPageTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static BunitContext NewContext(RoomManager rooms)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(rooms);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string>("party.getOrCreatePlayerId").SetResult(Alice.ToString());
        ctx.JSInterop.Setup<string>("party.getName").SetResult("Alice");
        return ctx;
    }

    [Test]
    public async Task A_game_switch_re_opens_the_page_and_carries_the_player_across()
    {
        var rooms = new RoomManager();
        using var ctx = NewContext(rooms);
        var flip7 = (RoomHandle<Flip7Room>)rooms.CreateRoom(GameType.Flip7);

        var page = ctx.Render<RoomPage>(p => p.Add(x => x.Code, flip7.Code));

        // The page opened on Flip 7 and joined Alice (host, connected).
        page.WaitForState(() => flip7.Read(r => r.Players.Any(p => p.Id == Alice && p.IsConnected)));

        await page.InvokeAsync(() => rooms.SwitchGame(flip7.Code, GameType.JustOne, Alice));

        rooms.TryGetRoom(flip7.Code, out var current);
        var justOne = (RoomHandle<GameRoom>)current!;

        // It followed the switch: connected to the new Just One room...
        page.WaitForState(() => justOne.Read(r => r.Players.Any(p => p.Id == Alice && p.IsConnected)));
        await Assert.That(justOne.Game).IsEqualTo(GameType.JustOne);
        // ...and no longer connected to the old Flip 7 room (its stage was disposed).
        await Assert.That(flip7.Read(r => r.Players.Single(p => p.Id == Alice).IsConnected)).IsFalse();
    }
}
