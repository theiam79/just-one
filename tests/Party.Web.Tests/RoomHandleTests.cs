using Party.Core;
using Party.Web.Services;

namespace Party.Web.Tests;

/// <summary>
/// The lock-and-fan-out layer every game's room sits behind. Until now nothing covered it,
/// which is awkward given a silent bug in this plumbing once made every rule violation in the
/// whole app fail quietly.
/// </summary>
public class RoomHandleTests
{
    /// <summary>A stand-in for a game's state machine — this layer never looks inside one.</summary>
    private sealed class FakeRoom
    {
        public int Value { get; set; }
    }

    private static RoomHandle<FakeRoom> NewHandle(FakeRoom? room = null) =>
        new("TEST", GameType.JustOne, room ?? new FakeRoom());

    [Test]
    public async Task Mutate_runs_the_action()
    {
        var handle = NewHandle();
        handle.Mutate(r => r.Value = 7);

        await Assert.That(handle.Read(r => r.Value)).IsEqualTo(7);
    }

    [Test]
    public async Task Mutate_raises_changed()
    {
        var handle = NewHandle();
        var raised = 0;
        handle.Changed += () => raised++;

        handle.Mutate(r => r.Value = 1);

        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task A_rejected_move_raises_nothing_and_propagates()
    {
        // This is why the page has to re-render itself after a rejected move: no event arrives.
        var handle = NewHandle();
        var raised = 0;
        handle.Changed += () => raised++;

        var error = Assert.Throws<GameRuleException>(() => handle.Mutate(_ => throw new GameRuleException("nope")));

        await Assert.That(error!.Message).IsEqualTo("nope");
        await Assert.That(raised).IsEqualTo(0);
    }

    [Test]
    public async Task Read_does_not_raise_changed()
    {
        var handle = NewHandle();
        var raised = 0;
        handle.Changed += () => raised++;

        handle.Read(r => r.Value);

        await Assert.That(raised).IsEqualTo(0);
    }

    [Test]
    public async Task One_dead_circuit_does_not_break_the_fan_out()
    {
        var handle = NewHandle();
        var reached = false;
        handle.Changed += () => throw new InvalidOperationException("this circuit is gone");
        handle.Changed += () => reached = true;

        handle.Mutate(r => r.Value = 1);

        await Assert.That(reached).IsTrue();
    }

    [Test]
    public async Task Unsubscribing_stops_the_notifications()
    {
        var handle = NewHandle();
        var raised = 0;
        Action handler = () => raised++;
        handle.Changed += handler;
        handle.Changed -= handler;

        handle.Mutate(r => r.Value = 1);

        await Assert.That(raised).IsEqualTo(0);
    }

    [Test]
    public async Task Mutating_a_closed_room_is_refused()
    {
        var handle = NewHandle();
        handle.Close();

        var error = Assert.Throws<GameRuleException>(() => handle.Mutate(r => r.Value = 1));
        await Assert.That(error!.Message).Contains("closed for inactivity");
    }

    [Test]
    public async Task Closing_tells_every_circuit()
    {
        var handle = NewHandle();
        var raised = 0;
        handle.Changed += () => raised++;

        handle.Close();

        await Assert.That(handle.IsClosed).IsTrue();
        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task A_closed_room_can_still_be_read()
    {
        // The page still needs to render something after the janitor sweeps the room.
        var handle = NewHandle(new FakeRoom { Value = 3 });
        handle.Close();

        await Assert.That(handle.Read(r => r.Value)).IsEqualTo(3);
    }

    [Test]
    public async Task Mutating_moves_the_idle_clock_along()
    {
        var handle = NewHandle();
        var before = handle.LastActivity;
        await Task.Delay(5);
        handle.Mutate(r => r.Value = 1);

        await Assert.That(handle.LastActivity).IsGreaterThan(before);
    }

    [Test]
    public async Task A_rejected_move_does_not_count_as_activity()
    {
        var handle = NewHandle();
        var before = handle.LastActivity;
        await Task.Delay(5);

        try
        {
            handle.Mutate(_ => throw new GameRuleException("nope"));
        }
        catch (GameRuleException)
        {
        }

        await Assert.That(handle.LastActivity).IsEqualTo(before);
    }

    [Test]
    public async Task Mutations_from_many_circuits_are_serialized()
    {
        // Read, yield, write: without the lock the interleaving loses updates every time. A
        // plain `Value++` would not — Parallel.For hands each thread a contiguous chunk, so
        // there is no contention to catch and the test would pass unlocked.
        var handle = NewHandle();
        var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                handle.Mutate(r =>
                {
                    var seen = r.Value;
                    Thread.Yield();
                    r.Value = seen + 1;
                });
            }
        })).ToList();

        foreach (var t in threads)
        {
            t.Start();
        }

        foreach (var t in threads)
        {
            t.Join();
        }

        await Assert.That(handle.Read(r => r.Value)).IsEqualTo(400);
    }

    [Test]
    public async Task A_slow_mutation_holds_everyone_else_off()
    {
        var handle = NewHandle();
        var inside = new ManualResetEventSlim();
        var overlapped = false;

        var slow = Task.Run(() => handle.Mutate(_ =>
        {
            inside.Set();
            Thread.Sleep(200);
        }));

        inside.Wait();
        var other = Task.Run(() =>
        {
            handle.Mutate(_ => { });
            overlapped = !slow.IsCompleted;
        });

        await Task.WhenAll(slow, other);
        await Assert.That(overlapped).IsFalse();
    }

    [Test]
    public async Task Changed_is_raised_outside_the_lock()
    {
        // Handlers run on the mutating thread; if the raise happened under the lock, a handler
        // could not be interrupted by another circuit's move and this would deadlock or block.
        var handle = NewHandle();
        var otherGotIn = false;
        var armed = true;

        handle.Changed += () =>
        {
            if (!armed)
            {
                return;   // the nested move raises this too; only probe once
            }

            armed = false;
            var other = Task.Run(() => handle.Mutate(r => r.Value = 99));
            otherGotIn = other.Wait(TimeSpan.FromSeconds(2));
        };

        handle.Mutate(r => r.Value = 1);

        await Assert.That(otherGotIn).IsTrue();
    }

    [Test]
    public async Task The_handle_knows_its_game()
    {
        RoomHandle handle = new RoomHandle<FakeRoom>("TEST", GameType.Flip7, new FakeRoom());
        await Assert.That(handle.Game).IsEqualTo(GameType.Flip7);
        await Assert.That(handle.Code).IsEqualTo("TEST");
    }
}
