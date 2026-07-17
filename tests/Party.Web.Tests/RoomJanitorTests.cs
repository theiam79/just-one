using Party.Web.Services;

namespace Party.Web.Tests;

/// <summary>
/// The janitor never learned there is more than one game, and shouldn't have to: it only ever
/// looks at a room's code and its idle clock.
/// </summary>
public class RoomJanitorTests
{
    private static readonly TimeSpan IdleLimit = TimeSpan.FromMilliseconds(50);

    /// <summary>The real janitor's real sweep, called directly so nothing races a timer.</summary>
    private static void Sweep(RoomManager manager) =>
        new RoomJanitor(manager, idleLimit: IdleLimit).Sweep();

    private static Task GoQuiet() => Task.Delay(IdleLimit * 3);

    [Test]
    public async Task It_evicts_an_idle_room()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);

        await GoQuiet();
        Sweep(manager);

        await Assert.That(manager.TryGetRoom(handle.Code, out _)).IsFalse();
        await Assert.That(handle.IsClosed).IsTrue();
    }

    [Test]
    public async Task Playing_holds_a_room_open()
    {
        // The room goes quiet for longer than the limit, then somebody moves. That move must
        // save it — which is the actual rule, rather than "a brand new room isn't reaped".
        var manager = new RoomManager();
        var handle = (RoomHandle<Party.JustOne.GameRoom>)manager.CreateRoom(GameType.JustOne);

        await GoQuiet();
        handle.Mutate(_ => { });
        Sweep(manager);

        await Assert.That(manager.TryGetRoom(handle.Code, out _)).IsTrue();
        await Assert.That(handle.IsClosed).IsFalse();
    }

    [Test]
    public async Task A_room_nobody_touched_is_swept()
    {
        // The same room, the same sweep, without the move. This is the pair to the test above:
        // together they pin eviction to activity rather than to age.
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);

        await GoQuiet();
        Sweep(manager);

        await Assert.That(handle.IsClosed).IsTrue();
    }

    [Test]
    public async Task It_evicts_rooms_of_any_game()
    {
        var manager = new RoomManager();
        var justOne = manager.CreateRoom(GameType.JustOne);
        var flip7 = manager.CreateRoom(GameType.Flip7);

        await GoQuiet();
        Sweep(manager);

        await Assert.That(justOne.IsClosed).IsTrue();
        await Assert.That(flip7.IsClosed).IsTrue();
        await Assert.That(manager.Rooms).IsEmpty();
    }

    [Test]
    public async Task An_evicted_room_stops_accepting_moves()
    {
        var manager = new RoomManager();
        var handle = (RoomHandle<Party.Flip7.Flip7Room>)manager.CreateRoom(GameType.Flip7);

        await GoQuiet();
        Sweep(manager);

        var error = Assert.Throws<Party.Core.GameRuleException>(() => handle.Mutate(_ => { }));
        await Assert.That(error!.Message).Contains("closed");
    }

    [Test]
    public async Task Eviction_tells_the_players_still_looking_at_it()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);
        var told = 0;
        handle.Changed += () => told++;

        await GoQuiet();
        Sweep(manager);

        await Assert.That(told).IsGreaterThanOrEqualTo(1);
    }
}
