namespace Party.Web.Services;

/// <summary>
/// A seat as the shared player list needs it: enough to draw who is here, who is host, and who
/// has wandered off, and nothing about what is being played.
/// </summary>
/// <remarks>
/// Each game projects its own richer player view down to this. Whatever else a game wants to
/// show against a name — a guesser's target, a bust, a running total — it supplies as a badge
/// fragment, so the list itself never learns about either game.
/// </remarks>
public sealed record RosterEntry(
    Guid Id,
    string Name,
    bool IsHost,
    bool IsConnected,
    bool IsSpectator,
    bool IsBenched);
