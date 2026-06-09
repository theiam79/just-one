using JustOne.Core;

namespace JustOne.Web.Services;

public enum ClueStatus { NotApplicable, Writing, Done, Skipped }

public sealed record PlayerView(Guid Id, string Name, bool IsHost, bool IsConnected, bool IsSpectator, bool IsGuesser, ClueStatus ClueStatus);

public sealed record ClueView(Guid AuthorId, string AuthorName, string Text, bool AutoCancelled, bool ManuallyCancelled)
{
    public bool Cancelled => AutoCancelled || ManuallyCancelled;
}

/// <summary>
/// An immutable, per-viewer snapshot of a room, taken under the room lock.
/// All information hiding happens here: the guesser never receives the mystery word
/// or unrevealed clue texts, so there is nothing to cheat with even in dev tools.
/// </summary>
public sealed record RoomView
{
    public required string Code { get; init; }
    public required GamePhase Phase { get; init; }
    public required Guid MyId { get; init; }
    public required IReadOnlyList<PlayerView> Players { get; init; }
    public required int Score { get; init; }
    public required int CardsLeft { get; init; }
    public required bool IAmHost { get; init; }
    public required bool HasHostPowers { get; init; }
    public required bool IAmSpectator { get; init; }
    public required bool IAmGuesser { get; init; }
    public required bool IAmExpectedWriter { get; init; }
    public required string? HostName { get; init; }
    public required int RoundNumber { get; init; }
    public required string? GuesserName { get; init; }
    public required string? MysteryWord { get; init; }
    public required string? MyClueText { get; init; }
    public required IReadOnlyList<ClueView> Clues { get; init; }
    public required IReadOnlyList<PlayerView> PendingWriters { get; init; }
    public required string? Guess { get; init; }
    public required RoundOutcome? Outcome { get; init; }
    public required IReadOnlyList<RoundRecord> CompletedRounds { get; init; }
    public required IReadOnlyList<GameSummary> History { get; init; }

    public int VisibleClueCount => Clues.Count(c => !c.Cancelled);

    public static RoomView Build(GameRoom room, Guid viewerId)
    {
        var round = room.Round;
        var me = room.Players.FirstOrDefault(p => p.Id == viewerId);
        var host = room.Host;
        var iAmGuesser = round is not null && round.GuesserId == viewerId;
        var names = room.Players.ToDictionary(p => p.Id, p => p.Name);

        var players = room.Players
            .Select(p => new PlayerView(
                p.Id,
                p.Name,
                p.IsHost,
                p.IsConnected,
                p.IsSpectator,
                round is not null && room.Phase is not (GamePhase.Lobby or GamePhase.GameOver) && round.GuesserId == p.Id,
                ClueStatusOf(room, round, p.Id)))
            .ToList();

        return new RoomView
        {
            Code = room.Code,
            Phase = room.Phase,
            MyId = viewerId,
            Players = players,
            Score = room.Score,
            CardsLeft = room.CardsLeft,
            IAmHost = me?.IsHost ?? false,
            HasHostPowers = me is not null && (me.IsHost || host is null || !host.IsConnected),
            IAmSpectator = me?.IsSpectator ?? true,
            IAmGuesser = iAmGuesser,
            IAmExpectedWriter = round is not null && round.ExpectedWriters.Contains(viewerId),
            HostName = host?.Name,
            RoundNumber = round?.RoundNumber ?? 0,
            GuesserName = round is null ? null : names.GetValueOrDefault(round.GuesserId, "?"),
            MysteryWord = round?.MysteryWord is { } word && (!iAmGuesser || room.Phase is GamePhase.RoundResult or GamePhase.GameOver) ? word : null,
            MyClueText = round?.Clues.GetValueOrDefault(viewerId)?.Text,
            Clues = CluesFor(room, round, iAmGuesser, names),
            PendingWriters = round is null
                ? []
                : [.. players.Where(p => round.ExpectedWriters.Contains(p.Id) && !round.Clues.ContainsKey(p.Id) && !round.SkippedWriters.Contains(p.Id))],
            Guess = round?.Guess,
            Outcome = round?.Outcome,
            CompletedRounds = [.. room.CompletedRounds],
            History = [.. room.History],
        };
    }

    private static ClueStatus ClueStatusOf(GameRoom room, RoundState? round, Guid playerId)
    {
        if (round is null || room.Phase != GamePhase.ClueWriting || !round.ExpectedWriters.Contains(playerId))
        {
            return ClueStatus.NotApplicable;
        }

        if (round.Clues.ContainsKey(playerId))
        {
            return ClueStatus.Done;
        }

        return round.SkippedWriters.Contains(playerId) ? ClueStatus.Skipped : ClueStatus.Writing;
    }

    private static IReadOnlyList<ClueView> CluesFor(GameRoom room, RoundState? round, bool iAmGuesser, Dictionary<Guid, string> names)
    {
        if (round is null)
        {
            return [];
        }

        var all = round.Clues.Values
            .Select(c => new ClueView(c.AuthorId, names.GetValueOrDefault(c.AuthorId, "?"), c.Text, c.AutoCancelled, c.ManuallyCancelled))
            .OrderBy(c => c.AuthorName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (iAmGuesser)
        {
            return room.Phase switch
            {
                GamePhase.Guessing or GamePhase.Judging => [.. all.Where(c => !c.Cancelled)],
                GamePhase.RoundResult => all,
                _ => [],
            };
        }

        return room.Phase is GamePhase.ClueReview or GamePhase.Guessing or GamePhase.Judging or GamePhase.RoundResult ? all : [];
    }
}
