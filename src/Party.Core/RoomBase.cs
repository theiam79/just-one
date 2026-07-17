namespace Party.Core;

/// <summary>
/// The roster every room has, whatever is being played on it: who is here, who is the host,
/// who is watching, and who has wandered off.
/// </summary>
/// <remarks>
/// Deliberately not a universal game: there is no shared phase, no shared score, and no shared
/// notion of a round. Just One's phases mean nothing to a push-your-luck flip/bust/stay loop
/// and vice versa, so each game keeps its own state machine and this owns only the seats.
/// <para>
/// The connection handling is the fiddly part and the reason this is worth sharing. The bench
/// is <em>sticky</em>: it lifts while a player is genuinely connected and re-arms the moment
/// they drop, so a flaky circuit can't quietly deal them back in.
/// </para>
/// </remarks>
public abstract class RoomBase(string code)
{
    private readonly List<Player> _players = [];
    private readonly List<ChatMessage> _chat = [];
    private int _chatSeq;

    public const int MaxNameLength = 20;

    /// <summary>Longest a single chat message may be; anything over is trimmed to fit.</summary>
    public const int MaxChatLength = 500;

    /// <summary>How many recent messages a room keeps. Old ones fall off — chat is ephemeral.</summary>
    private const int ChatHistory = 200;

    public string Code { get; } = code;

    public IReadOnlyList<Player> Players => _players;

    public Player? Host => _players.FirstOrDefault(p => p.IsHost);

    /// <summary>The room's chat, oldest first. Shared by every game — it's just people talking.</summary>
    public IReadOnlyList<ChatMessage> Chat => _chat;

    /// <summary>
    /// Posts a chat message from someone in the room. Blank messages are ignored and over-long
    /// ones are trimmed, so this never rejects a real attempt to talk — anyone seated or watching
    /// can chat.
    /// </summary>
    public void PostChat(Guid callerId, string text)
    {
        var sender = GetPlayer(callerId);   // must be in the room to talk in it
        text = text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (text.Length > MaxChatLength)
        {
            text = text[..MaxChatLength];
        }

        _chat.Add(new ChatMessage(_chatSeq++, sender.Id, sender.Name, text));
        if (_chat.Count > ChatHistory)
        {
            _chat.RemoveAt(0);
        }
    }

    /// <summary>Fewest players this game will start with.</summary>
    protected abstract int MinSeats { get; }

    /// <summary>Most seats this game's table holds.</summary>
    protected abstract int MaxSeats { get; }

    /// <summary>
    /// Whether someone arriving right now is dealt in. When false they watch until the next
    /// game — which is every game's answer to "you can't join a hand already in progress".
    /// </summary>
    protected abstract bool DealsInNewPlayers { get; }

    /// <summary>
    /// Whether a player leaving gives up their seat outright. While a game is running the seat
    /// is kept, because scores and history still point at it.
    /// </summary>
    protected abstract bool SeatsAreFree { get; }

    /// <summary>
    /// Called when a player stops being expected to do anything — they left, or the host sat
    /// them out — while a game is in progress. Each game unblocks itself here: Just One stops
    /// waiting on their clue, Flip 7 banks their line so the turn can move on.
    /// </summary>
    protected virtual void OnPlayerSidelined(Guid id)
    {
    }

    /// <summary>
    /// Called after a player's connection count changes. A turn-based game uses this to play on
    /// rather than stall on someone who has gone.
    /// </summary>
    protected virtual void OnConnectionChanged(Guid id)
    {
    }

    public Player Join(Guid id, string name)
    {
        name = name.Trim();
        if (name.Length > MaxNameLength)
        {
            name = name[..MaxNameLength];
        }

        var existing = _players.FirstOrDefault(p => p.Id == id);
        if (existing is not null)
        {
            if (name.Length > 0)
            {
                existing.Name = name;
            }

            return existing;
        }

        if (name.Length == 0)
        {
            throw new GameRuleException("Enter a name first.");
        }

        if (_players.Count >= MaxSeats)
        {
            throw new GameRuleException($"This room is full ({MaxSeats} players max).");
        }

        var player = new Player
        {
            Id = id,
            Name = name,
            IsHost = _players.Count == 0,
            IsSpectator = !DealsInNewPlayers,
        };
        _players.Add(player);
        return player;
    }

    public void Leave(Guid id)
    {
        var player = _players.FirstOrDefault(p => p.Id == id);
        if (player is null)
        {
            return;
        }

        if (SeatsAreFree || player.IsSpectator)
        {
            _players.Remove(player);
        }
        else
        {
            // Mid-game: keep the seat (scores still point at it) but stop expecting anything.
            player.IsSpectator = true;
            OnPlayerSidelined(id);
        }

        if (player.IsHost)
        {
            player.IsHost = false;
            var next = _players.FirstOrDefault(p => !p.IsSpectator) ?? _players.FirstOrDefault();
            if (next is not null)
            {
                next.IsHost = true;
            }
        }
    }

    /// <summary>
    /// Sits an away player out until they come back, so the host doesn't have to work around
    /// them every round. The decision is sticky — it lifts while they're actually connected and
    /// re-arms if they drop again — and clears once a new game starts with them present.
    /// </summary>
    public void BenchPlayer(Guid callerId, Guid targetId)
    {
        RequireHostPowers(callerId);
        var player = _players.FirstOrDefault(p => p.Id == targetId)
            ?? throw new GameRuleException("That player isn't in this room.");

        if (player.IsConnected)
        {
            throw new GameRuleException("You can only sit out a player who's away.");
        }

        if (player.IsSpectator)
        {
            throw new GameRuleException("They're already sitting out.");
        }

        player.IsSpectator = true;
        player.BenchedForInactivity = true;
        OnPlayerSidelined(targetId);
    }

    public void PlayerConnected(Guid id)
    {
        var player = _players.FirstOrDefault(p => p.Id == id);
        if (player is null)
        {
            return;
        }

        player.ConnectionCount++;
        if (player.BenchedForInactivity)
        {
            // They're back — playing again from the next round. The bench decision itself is
            // kept so a brief reconnect doesn't discard it; it re-arms below if they drop again,
            // and ClearSpectators retires it once a new game starts with them here.
            player.IsSpectator = false;
        }

        OnConnectionChanged(id);
    }

    public void PlayerDisconnected(Guid id)
    {
        var player = _players.FirstOrDefault(p => p.Id == id);
        if (player is null || player.ConnectionCount == 0)
        {
            return;
        }

        player.ConnectionCount--;
        if (player.BenchedForInactivity && !player.IsConnected)
        {
            // Away again, and the host already sat them out: don't make them re-do it.
            player.IsSpectator = true;
        }

        OnConnectionChanged(id);
    }

    /// <summary>Whether a new game would leave this player sitting out: benched, and still away.</summary>
    protected static bool StaysBenched(Player p) => p.BenchedForInactivity && !p.IsConnected;

    /// <summary>Everyone who would be dealt into a game starting now.</summary>
    protected IEnumerable<Player> Seated => _players.Where(p => !p.IsSpectator);

    /// <summary>
    /// Brings spectators in as players for a new game — except anyone benched for inactivity
    /// who is still away, who stays out until they actually come back. Players who are back
    /// have their bench retired, so the next game is a clean slate for them.
    /// </summary>
    protected void ClearSpectators()
    {
        foreach (var p in _players)
        {
            if (StaysBenched(p))
            {
                continue;
            }

            p.IsSpectator = false;
            p.BenchedForInactivity = false;
        }
    }

    /// <summary>Refuses to start a game nobody could actually play.</summary>
    protected void RequireEnoughPlayers()
    {
        // Players benched while away sit the next game out, so they don't count towards the
        // minimum — otherwise a "full" room could start a game nobody can actually play.
        if (_players.Count(p => !StaysBenched(p)) < MinSeats)
        {
            throw new GameRuleException($"Need at least {MinSeats} players to start.");
        }
    }

    protected Player GetPlayer(Guid id) =>
        _players.FirstOrDefault(p => p.Id == id) ?? throw new GameRuleException("You're not in this room.");

    /// <summary>Anyone actually playing, as opposed to watching.</summary>
    protected void RequireSeated(Guid callerId)
    {
        if (GetPlayer(callerId).IsSpectator)
        {
            throw new GameRuleException("Spectators can't do that — you're in next game!");
        }
    }

    /// <summary>
    /// Whether this caller may use a host power right now: they're the host, or the host is
    /// away so anyone may drive rather than let the game stall. False for a stranger.
    /// </summary>
    public bool CanActAsHost(Guid callerId)
    {
        var caller = _players.FirstOrDefault(p => p.Id == callerId);
        if (caller is null)
        {
            return false;
        }

        if (caller.IsHost)
        {
            return true;
        }

        var host = Host;
        return host is null || !host.IsConnected;
    }

    /// <summary>Host-only, but if the host is disconnected anyone may drive so the game never stalls.</summary>
    protected void RequireHostPowers(Guid callerId)
    {
        GetPlayer(callerId);   // must at least be in the room
        if (CanActAsHost(callerId))
        {
            return;
        }

        throw new GameRuleException($"Only the host ({Host!.Name}) can do that.");
    }
}
