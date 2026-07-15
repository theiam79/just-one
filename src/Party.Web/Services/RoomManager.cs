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
            RoomHandle handle = game switch
            {
                GameType.Flip7 => new RoomHandle<Flip7Room>(code, game, Flip7Room.Standard(code, Random.Shared)),
                GameType.JustOne => new RoomHandle<GameRoom>(code, game, new GameRoom(code, WordList.Words, Random.Shared)),
                _ => throw new ArgumentOutOfRangeException(nameof(game)),
            };

            if (_rooms.TryAdd(code, handle))
            {
                return handle;
            }
        }
    }

    public bool TryGetRoom(string? code, [NotNullWhen(true)] out RoomHandle? handle)
    {
        handle = null;
        return !string.IsNullOrWhiteSpace(code) && _rooms.TryGetValue(code.Trim(), out handle);
    }

    public IReadOnlyCollection<RoomHandle> Rooms => (IReadOnlyCollection<RoomHandle>)_rooms.Values;

    public void Remove(string code) => _rooms.TryRemove(code, out _);
}
