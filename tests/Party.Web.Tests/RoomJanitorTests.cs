using Microsoft.Extensions.Hosting;
using Party.Web.Services;

namespace Party.Web.Tests;

/// <summary>
/// The janitor never learned there is more than one game, and shouldn't have to: it only ever
/// looks at a room's code and its idle clock.
/// </summary>
public class RoomJanitorTests
{
    /// <summary>
    /// Runs the real janitor, briefly. Its sweep interval is minutes long, so rather than wait
    /// we start it, let it take its first look, and stop it.
    /// </summary>
    private static async Task Sweep(RoomManager manager)
    {
        var janitor = new RoomJanitor(manager, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30));
        await ((IHostedService)janitor).StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await ((IHostedService)janitor).StopAsync(CancellationToken.None);
        janitor.Dispose();
    }

    [Test]
    public async Task It_evicts_an_idle_room()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);

        await Task.Delay(60);   // older than the idle limit above
        await Sweep(manager);

        await Assert.That(manager.TryGetRoom(handle.Code, out _)).IsFalse();
        await Assert.That(handle.IsClosed).IsTrue();
    }

    [Test]
    public async Task It_leaves_a_room_that_is_still_in_use_alone()
    {
        // A generous idle limit rather than a keep-alive loop racing the scheduler: the room is
        // simply younger than the limit, so no amount of sweeping should touch it.
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);

        var janitor = new RoomJanitor(manager, TimeSpan.FromMilliseconds(20), TimeSpan.FromHours(1));
        await ((IHostedService)janitor).StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await ((IHostedService)janitor).StopAsync(CancellationToken.None);
        janitor.Dispose();

        await Assert.That(manager.TryGetRoom(handle.Code, out _)).IsTrue();
        await Assert.That(handle.IsClosed).IsFalse();
    }

    [Test]
    public async Task It_evicts_rooms_of_any_game()
    {
        var manager = new RoomManager();
        var justOne = manager.CreateRoom(GameType.JustOne);
        var flip7 = manager.CreateRoom(GameType.Flip7);

        await Task.Delay(60);
        await Sweep(manager);

        await Assert.That(justOne.IsClosed).IsTrue();
        await Assert.That(flip7.IsClosed).IsTrue();
        await Assert.That(manager.Rooms).IsEmpty();
    }

    [Test]
    public async Task An_evicted_room_stops_accepting_moves()
    {
        var manager = new RoomManager();
        var handle = (RoomHandle<Party.Flip7.Flip7Room>)manager.CreateRoom(GameType.Flip7);

        await Task.Delay(60);
        await Sweep(manager);

        var error = Assert.Throws<Party.Core.GameRuleException>(() => handle.Mutate(_ => { }));
        await Assert.That(error!.Message).Contains("closed for inactivity");
    }

    [Test]
    public async Task Eviction_tells_the_players_still_looking_at_it()
    {
        var manager = new RoomManager();
        var handle = manager.CreateRoom(GameType.JustOne);
        var told = 0;
        handle.Changed += () => told++;

        await Task.Delay(60);
        await Sweep(manager);

        await Assert.That(told).IsGreaterThanOrEqualTo(1);
    }
}
