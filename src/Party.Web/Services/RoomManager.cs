using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Party.Core;
using Party.Flip7;
using Party.JustOne;

namespace Party.Web.Services;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, RoomHandle> _rooms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One code space across every game: a player types a code and lands in whichever game it
    /// turns out to be, without being asked which game they are joining.
    /// </summary>
    public RoomHandle CreateRoom(GameType game)
    {
        while (true)
        {
            var code = RoomCode.New(Random.Shared);
            var handle = CreateHandle(code, game);
            if (_rooms.TryAdd(code, handle))
            {
                return handle;
            }
        }
    }

    private static RoomHandle CreateHandle(string code, GameType game) => game switch
    {
        GameType.Flip7 => new RoomHandle<Flip7Room>(code, game, Flip7Room.Standard(code, Random.Shared)),
        GameType.JustOne => new RoomHandle<GameRoom>(code, game, new GameRoom(code, WordList.Words, Random.Shared)),
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };

    /// <summary>
    /// Switches a room to a different game, keeping the code and the players. Host-only. The new
    /// room starts fresh in its lobby with the roster carried over; the old handle is superseded
    /// so every circuit re-opens on the new game. Returns the new handle, or null if the caller
    /// can't switch or the room's gone.
    /// </summary>
    public RoomHandle? SwitchGame(string code, GameType newGame, Guid callerId)
    {
        if (!TryGetRoom(code, out var old))
        {
            return null;
        }

        if (old.Game == newGame)
        {
            return old;   // already there
        }

        if (!old.CanSwitchGame(callerId))
        {
            return null;   // host power required; the UI already gates this
        }

        var handle = CreateHandle(old.Code, newGame);
        handle.AdoptPlayers(old.SnapshotPlayers());

        // Swap only if the code still points at the room we snapshotted — otherwise it was closed,
        // removed, or already switched out from under us, and we mustn't resurrect it.
        if (!_rooms.TryUpdate(old.Code, handle, old))
        {
            return null;
        }

        old.MarkSuperseded();
        return handle;
    }

    public bool TryGetRoom(string? code, [NotNullWhen(true)] out RoomHandle? handle)
    {
        handle = null;
        return !string.IsNullOrWhiteSpace(code) && _rooms.TryGetValue(code.Trim(), out handle);
    }

    public IReadOnlyCollection<RoomHandle> Rooms => (IReadOnlyCollection<RoomHandle>)_rooms.Values;

    public void Remove(string code) => _rooms.TryRemove(code, out _);
}
