namespace Party.Core;

/// <summary>
/// One line in a game's event log: a sequence number, what happened in words, and a category the
/// UI can style by. The category is an opaque string here — each game names its own.
/// </summary>
public sealed record GameLogEntry(int Sequence, string Text, string Category);

/// <summary>
/// A running, ordered record of what happened, in words. The mechanism is deliberately
/// game-agnostic: it is just a list of categorised lines a game appends to, oldest first. What
/// counts as an event, and how each line reads, is the game's to decide — this owns none of it.
/// </summary>
/// <remarks>
/// Scoping (per round, per game, forever) is the caller's call too: a game that wants a
/// per-round log simply <see cref="Clear"/>s it when a round starts.
/// </remarks>
public sealed class GameLog
{
    private readonly List<GameLogEntry> _entries = [];

    public IReadOnlyList<GameLogEntry> Entries => _entries;

    public void Add(string text, string category = "info") =>
        _entries.Add(new GameLogEntry(_entries.Count, text, category));

    public void Clear() => _entries.Clear();
}
