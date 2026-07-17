using Party.Core;

namespace Party.Web.Services;

/// <summary>Why a room shut: the janitor's idle sweep, or a host ending it on purpose.</summary>
public enum RoomCloseReason
{
    Inactivity,
    HostClosed,
}

/// <summary>
/// Serializes all access to one room and fans out change notifications to every subscribed
/// circuit.
/// </summary>
/// <remarks>
/// Mutations run under the lock and <see cref="Changed"/> is raised outside it, so a handler
/// can't block the room while it works. Handlers should only schedule a re-render: the lock is
/// re-entrant, so one that calls straight back into
/// <see cref="RoomHandle{TRoom}.Mutate"/> won't deadlock — it will quietly recurse instead,
/// raising <see cref="Changed"/> again on the way.
/// </remarks>
/// <remarks>
/// The non-generic half is everything that doesn't care which game is being played — the code,
/// the idle clock, closing, and the change event. That's what lets one dictionary of rooms and
/// the janitor sweeping it stay oblivious to the game inside.
/// </remarks>
public abstract class RoomHandle
{
    // Only the generic subclass below is a room; nothing else should inherit this.
    private protected RoomHandle()
    {
    }

    private protected readonly object Gate = new();

    public abstract string Code { get; }

    /// <summary>Which game this room is playing, so the page knows what to render.</summary>
    public abstract GameType Game { get; }

    public DateTimeOffset LastActivity { get; private protected set; } = DateTimeOffset.UtcNow;

    public bool IsClosed { get; private set; }

    /// <summary>Why the room closed. Only meaningful once <see cref="IsClosed"/> is true.</summary>
    public RoomCloseReason CloseReason { get; private set; }

    public event Action? Changed;

    public void Close(RoomCloseReason reason = RoomCloseReason.Inactivity)
    {
        lock (Gate)
        {
            IsClosed = true;
            CloseReason = reason;
        }

        RaiseChanged();
    }

    private protected void ThrowIfClosed()
    {
        if (IsClosed)
        {
            throw new GameRuleException("This room has been closed.");
        }
    }

    private protected void RaiseChanged()
    {
        foreach (var handler in Changed?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action)handler)();
            }
            catch
            {
                // One dead circuit must not break the fan-out to everyone else.
            }
        }
    }
}

/// <summary>A room handle that knows its game's state machine.</summary>
public sealed class RoomHandle<TRoom>(string code, GameType game, TRoom room) : RoomHandle
{
    public override string Code => code;

    public override GameType Game => game;

    public void Mutate(Action<TRoom> action)
    {
        lock (Gate)
        {
            ThrowIfClosed();
            action(room);
            LastActivity = DateTimeOffset.UtcNow;
        }

        RaiseChanged();
    }

    public T Read<T>(Func<TRoom, T> selector)
    {
        lock (Gate)
        {
            return selector(room);
        }
    }
}
