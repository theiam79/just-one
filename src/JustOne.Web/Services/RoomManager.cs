using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using JustOne.Core;

namespace JustOne.Web.Services;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, RoomHandle> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public RoomHandle CreateRoom()
    {
        while (true)
        {
            var code = RoomCode.New(Random.Shared);
            var handle = new RoomHandle(new GameRoom(code, WordList.Words, Random.Shared));
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
