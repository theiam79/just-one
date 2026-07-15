using Party.Core;
using Party.Flip7;
using Party.JustOne;
using Party.Web.Services;

namespace Party.Web.Tests;

public class RoomManagerTests
{
    [Test]
    public async Task Creates_a_just_one_room()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);

        await Assert.That(handle.Game).IsEqualTo(GameType.JustOne);
        await Assert.That(handle).IsTypeOf<RoomHandle<GameRoom>>();
    }

    [Test]
    public async Task Creates_a_flip7_room()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.Flip7);

        await Assert.That(handle.Game).IsEqualTo(GameType.Flip7);
        await Assert.That(handle).IsTypeOf<RoomHandle<Flip7Room>>();
    }

    [Test]
    public async Task A_new_room_carries_its_own_code()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.Flip7);

        await Assert.That(handle.Code.Length).IsEqualTo(RoomCode.Length);
        var code = ((RoomHandle<Flip7Room>)handle).Read(r => r.Code);
        await Assert.That(code).IsEqualTo(handle.Code);
    }

    [Test]
    public async Task Both_games_share_one_code_space()
    {
        // A player types a code and lands in whichever game it is — no "which game?" step.
        var manager = new RoomManager();
        var justOne = manager.CreateRoom(GameType.JustOne);
        var flip7 = manager.CreateRoom(GameType.Flip7);

        await Assert.That(manager.TryGetRoom(justOne.Code, out var a)).IsTrue();
        await Assert.That(a!.Game).IsEqualTo(GameType.JustOne);

        await Assert.That(manager.TryGetRoom(flip7.Code, out var b)).IsTrue();
        await Assert.That(b!.Game).IsEqualTo(GameType.Flip7);
    }

    [Test]
    public async Task Codes_are_unique_across_games()
    {
        var manager = new RoomManager();
        var codes = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            codes.Add(manager.CreateRoom(i % 2 == 0 ? GameType.JustOne : GameType.Flip7).Code);
        }

        await Assert.That(codes.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(100);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public async Task A_blank_code_finds_nothing(string? code)
    {
        var manager = new RoomManager();
        await Assert.That(manager.TryGetRoom(code, out _)).IsFalse();
    }

    [Test]
    public async Task An_unknown_code_finds_nothing()
    {
        var manager = new RoomManager();
        await Assert.That(manager.TryGetRoom("ZZZZ", out _)).IsFalse();
    }

    [Test]
    public async Task Codes_are_matched_ignoring_case_and_padding()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);

        await Assert.That(manager.TryGetRoom(handle.Code.ToLowerInvariant(), out _)).IsTrue();
        await Assert.That(manager.TryGetRoom($"  {handle.Code}  ", out _)).IsTrue();
    }

    [Test]
    public async Task Removing_a_room_forgets_it()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);
        manager.Remove(handle.Code);

        await Assert.That(manager.TryGetRoom(handle.Code, out _)).IsFalse();
    }

    [Test]
    public async Task Rooms_lists_every_game()
    {
        var manager = new RoomManager();
        manager.CreateRoom(GameType.JustOne);
        manager.CreateRoom(GameType.Flip7);

        await Assert.That(manager.Rooms.Count).IsEqualTo(2);
        await Assert.That(manager.Rooms.Select(r => r.Game).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Creating_rooms_concurrently_never_collides()
    {
        var manager = new RoomManager();
        var handles = new RoomHandle[200];
        Parallel.For(0, handles.Length, i => handles[i] = manager.CreateRoom(GameType.JustOne));

        await Assert.That(handles.Select(h => h.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .IsEqualTo(handles.Length);
        await Assert.That(manager.Rooms.Count).IsEqualTo(handles.Length);
    }

    [Test]
    public async Task A_just_one_room_starts_in_its_lobby()
    {
        var manager = new RoomManager();
        var handle = (RoomHandle<GameRoom>)manager.CreateRoom(GameType.JustOne);

        await Assert.That(handle.Read(r => r.Phase)).IsEqualTo(GamePhase.Lobby);
    }

    [Test]
    public async Task A_flip7_room_starts_in_its_lobby()
    {
        var manager = new RoomManager();
        var handle = (RoomHandle<Flip7Room>)manager.CreateRoom(GameType.Flip7);

        await Assert.That(handle.Read(r => r.Phase)).IsEqualTo(Flip7Phase.Lobby);
    }

    [Test]
    public async Task Every_game_on_the_picker_can_actually_be_created()
    {
        var manager = new RoomManager();
        foreach (var game in GameInfo.All)
        {
            var handle = manager.CreateRoom(game.Type);
            await Assert.That(handle.Game).IsEqualTo(game.Type);
        }
    }
}
