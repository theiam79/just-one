using JustOne.Core;

namespace JustOne.Web.Services;

/// <summary>
/// Serializes all access to one <see cref="GameRoom"/> and fans out change notifications
/// to every subscribed circuit. Events are raised outside the lock; handlers must only
/// schedule a re-render (never call back into <see cref="Mutate"/> synchronously).
/// </summary>
public sealed class RoomHandle(GameRoom room)
{
    private readonly object _gate = new();

    public string Code => room.Code;
    public DateTimeOffset LastActivity { get; private set; } = DateTimeOffset.UtcNow;
    public bool IsClosed { get; private set; }

    public event Action? Changed;

    public void Mutate(Action<GameRoom> action)
    {
        lock (_gate)
        {
            if (IsClosed)
            {
                throw new GameRuleException("This room has been closed for inactivity.");
            }

            action(room);
            LastActivity = DateTimeOffset.UtcNow;
        }

        RaiseChanged();
    }

    public T Read<T>(Func<GameRoom, T> selector)
    {
        lock (_gate)
        {
            return selector(room);
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            IsClosed = true;
        }

        RaiseChanged();
    }

    private void RaiseChanged()
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
