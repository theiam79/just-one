namespace Party.Core.Tests;

/// <summary>
/// The smallest thing that is a room: a roster and a notion of "a game is running", with no
/// game attached.
/// </summary>
/// <remarks>
/// The base's own tests can't go through either real game, because reaching a given roster state
/// there means playing Just One or Flip 7 to get to it — which tests those games, not this. This
/// fake exists so the shared contract can be exercised directly, and so the hooks can be
/// observed being called rather than inferred from a game's behaviour.
/// </remarks>
internal sealed class TestRoom(int minSeats = 3, int maxSeats = 12) : RoomBase("TEST")
{
    public bool GameRunning { get; set; }

    /// <summary>Stands in for a phase where seats are kept but nobody is dealt in — Just One's game-over.</summary>
    public bool GameFinished { get; set; }

    public List<Guid> Sidelined { get; } = [];

    public List<Guid> ConnectionChanges { get; } = [];

    protected override int MinSeats => minSeats;

    protected override int MaxSeats => maxSeats;

    protected override bool DealsInNewPlayers => !GameRunning;

    protected override bool SeatsAreFree => !GameRunning || GameFinished;

    protected override void OnPlayerSidelined(Guid id) => Sidelined.Add(id);

    protected override void OnConnectionChanged(Guid id) => ConnectionChanges.Add(id);

    public void Start()
    {
        RequireEnoughPlayers();
        ClearSpectators();
        GameRunning = true;
    }

    public void NewGame() => ClearSpectators();

    /// <summary>Exposes the protected narration hook so the feed's ordering can be tested directly.</summary>
    public void Say(string text, string category = "info") => Narrate(text, category);

    public void AsHost(Guid callerId) => RequireHostPowers(callerId);

    public void AsSeated(Guid callerId) => RequireSeated(callerId);

    public IReadOnlyList<Guid> SeatedIds => [.. Seated.Select(p => p.Id)];
}
