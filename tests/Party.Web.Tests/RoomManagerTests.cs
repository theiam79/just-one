using Party.Core;
using Party.Flip7;
using Party.JustOne;
using Party.Web.Services;

namespace Party.Web.Tests;

public class RoomManagerTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Test]
    public async Task Switching_games_keeps_the_code_the_players_and_the_host()
    {
        var manager = new RoomManager();
        var flip7 = (RoomHandle<Flip7Room>)manager.CreateRoom(GameType.Flip7);
        flip7.Mutate(r =>
        {
            r.Join(Alice, "Alice");   // first in -> host
            r.Join(Bob, "Bob");
            r.Join(Carol, "Carol");
        });

        var switched = manager.SwitchGame(flip7.Code, GameType.JustOne, Alice);

        await Assert.That(switched).IsNotNull();
        await Assert.That(switched!.Game).IsEqualTo(GameType.JustOne);
        await Assert.That(switched).IsTypeOf<RoomHandle<GameRoom>>();
        await Assert.That(switched.Code).IsEqualTo(flip7.Code);

        // The code now resolves to the new game, with the roster and host carried over.
        await Assert.That(manager.TryGetRoom(flip7.Code, out var current)).IsTrue();
        await Assert.That(ReferenceEquals(current, switched)).IsTrue();
        var room = (RoomHandle<GameRoom>)switched;
        await Assert.That(room.Read(r => r.Players.Select(p => p.Name).ToList()))
            .IsEquivalentTo(new[] { "Alice", "Bob", "Carol" });
        await Assert.That(room.Read(r => r.Host!.Name)).IsEqualTo("Alice");
    }

    [Test]
    public async Task Switching_supersedes_the_old_handle()
    {
        var manager = new RoomManager();
        var flip7 = (RoomHandle<Flip7Room>)manager.CreateRoom(GameType.Flip7);
        flip7.Mutate(r => r.Join(Alice, "Alice"));
        var superseded = false;
        flip7.Superseded += () => superseded = true;

        manager.SwitchGame(flip7.Code, GameType.JustOne, Alice);

        await Assert.That(superseded).IsTrue();
    }

    [Test]
    public async Task Only_the_host_can_switch_games()
    {
        var manager = new RoomManager();
        var flip7 = (RoomHandle<Flip7Room>)manager.CreateRoom(GameType.Flip7);
        flip7.Mutate(r =>
        {
            r.Join(Alice, "Alice");
            r.PlayerConnected(Alice);   // host present
            r.Join(Bob, "Bob");
            r.PlayerConnected(Bob);
        });

        var result = manager.SwitchGame(flip7.Code, GameType.JustOne, Bob);   // Bob isn't host

        await Assert.That(result).IsNull();
        await Assert.That(manager.TryGetRoom(flip7.Code, out var current)).IsTrue();
        await Assert.That(current!.Game).IsEqualTo(GameType.Flip7);   // unchanged
    }

    [Test]
    public async Task Switching_to_the_same_game_changes_nothing()
    {
        var manager = new RoomManager();
        var flip7 = manager.CreateRoom(GameType.Flip7);

        var result = manager.SwitchGame(flip7.Code, GameType.Flip7, Guid.NewGuid());

        await Assert.That(ReferenceEquals(result, flip7)).IsTrue();
    }

    [Test]
    public async Task Switching_an_unknown_room_returns_null()
    {
        var manager = new RoomManager();

        await Assert.That(manager.SwitchGame("ZZZZ", GameType.Flip7, Guid.NewGuid())).IsNull();
    }

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

    [Test]
    public async Task Every_game_gets_a_handle_of_the_matching_type()
    {
        // RoomPage dispatches on the handle's type, so a room must be built with the one that
        // fits its game or it would render the "can't open this room" fallback.
        var manager = new RoomManager();

        await Assert.That(manager.CreateRoom(GameType.JustOne)).IsTypeOf<RoomHandle<GameRoom>>();
        await Assert.That(manager.CreateRoom(GameType.Flip7)).IsTypeOf<RoomHandle<Flip7Room>>();
    }
}
