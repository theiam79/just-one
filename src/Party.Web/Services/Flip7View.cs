using Party.Core;
using Party.Flip7;

namespace Party.Web.Services;

/// <summary>One player as the table sees them. Everything here is public knowledge.</summary>
public sealed record Flip7PlayerView(
    Guid Id,
    string Name,
    bool IsHost,
    bool IsConnected,
    bool IsSpectator,
    bool IsBenched,
    bool IsDealer,
    bool IsTheirTurn,
    RoundStatus Status,
    IReadOnlyList<Card> Line,
    int RoundScore,
    int Total)
{
    /// <summary>Held, and worth nothing until it cancels a bust.</summary>
    public bool HasSecondChance => Line.Any(c => c is ActionCard { Kind: ActionKind.SecondChance });
}

/// <summary>
/// An immutable, per-viewer snapshot of a Flip 7 room, taken under the room lock.
/// </summary>
/// <remarks>
/// Unlike Just One's, this hides nothing: every card in Flip 7 is face up on the table, so the
/// snapshot is the same for everyone bar a handful of "is this me" flags. The per-viewer shape
/// is kept because that's how the app renders rooms, not because there's anything to conceal.
/// </remarks>
public sealed record Flip7View
{
    public required string Code { get; init; }
    public required Flip7Phase Phase { get; init; }
    public required Guid MyId { get; init; }
    public required IReadOnlyList<Flip7PlayerView> Players { get; init; }
    public required bool HasHostPowers { get; init; }
    public required bool IAmSpectator { get; init; }
    public required string? HostName { get; init; }
    public required int RoundNumber { get; init; }
    public required int DeckCount { get; init; }

    /// <summary>Whose go it is, or null when the round isn't waiting on anyone.</summary>
    public required Guid? CurrentPlayerId { get; init; }

    public required bool IAmCurrentPlayer { get; init; }

    /// <summary>The card this player has to place, if the round is waiting on them to choose.</summary>
    public required Card? MyChoiceCard { get; init; }

    public required ChoiceKind? MyChoiceKind { get; init; }

    /// <summary>Who this player may place it on.</summary>
    public required IReadOnlyList<Flip7PlayerView> MyChoiceTargets { get; init; }

    /// <summary>Who the table is waiting on to place a card, when it isn't this player.</summary>
    public required string? ChoosingPlayerName { get; init; }

    public required Guid? Flip7PlayerId { get; init; }
    public required Guid? WinnerId { get; init; }

    /// <summary>What has happened this round, oldest first. Empty in the lobby.</summary>
    public required IReadOnlyList<GameLogEntry> Log { get; init; }

    public bool IAmChoosing => MyChoiceCard is not null;

    public Flip7PlayerView? Me => Players.FirstOrDefault(p => p.Id == MyId);

    /// <summary>A player with nothing in front of them can't stay — hitting is their only move.</summary>
    public bool CanStay => Me is { Line.Count: > 0 };

    public IReadOnlyList<Flip7PlayerView> Standings => [.. Players.OrderByDescending(p => p.Total).ThenBy(p => p.Name)];

    /// <summary>The seats, as the shared player list wants them.</summary>
    public IReadOnlyList<RosterEntry> Roster =>
        [.. Players.Select(p => new RosterEntry(p.Id, p.Name, p.IsHost, p.IsConnected, p.IsSpectator, p.IsBenched))];

    public Flip7PlayerView? Seat(Guid id) => Players.FirstOrDefault(p => p.Id == id);

    public static Flip7View Build(Flip7Room room, Guid viewerId)
    {
        var round = room.Round;
        var me = room.Players.FirstOrDefault(p => p.Id == viewerId);
        var host = room.Host;
        var choice = room.PendingChoice;

        var players = room.Players
            .Select(p =>
            {
                var hand = round?.Hands.GetValueOrDefault(p.Id);
                IReadOnlyList<Card> line = hand is null ? [] : [.. hand.Tableau.Cards];
                return new Flip7PlayerView(
                    p.Id,
                    p.Name,
                    p.IsHost,
                    p.IsConnected,
                    p.IsSpectator,
                    p.BenchedForInactivity && p.IsSpectator,
                    round is not null && round.DealerId == p.Id,
                    round is not null && round.CurrentPlayerId == p.Id,
                    hand?.Status ?? RoundStatus.Active,
                    line,
                    hand is null ? 0 : Flip7Rules.Score(hand.Tableau, !hand.Scores),
                    room.Totals.GetValueOrDefault(p.Id));
            })
            .ToList();

        var targets = choice is null
            ? []
            : room.LegalChoiceTargets.Select(id => players.First(p => p.Id == id)).ToList();

        return new Flip7View
        {
            Code = room.Code,
            Phase = room.Phase,
            MyId = viewerId,
            Players = players,
            HasHostPowers = (me?.IsHost ?? false) || host is null || !host.IsConnected,
            IAmSpectator = me?.IsSpectator ?? true,
            HostName = host?.Name,
            RoundNumber = round?.RoundNumber ?? 0,
            DeckCount = room.DeckCount,
            CurrentPlayerId = round?.CurrentPlayerId,
            IAmCurrentPlayer = round?.CurrentPlayerId == viewerId,
            MyChoiceCard = choice?.ChooserId == viewerId ? choice.Card : null,
            MyChoiceKind = choice?.ChooserId == viewerId ? choice.Kind : null,
            MyChoiceTargets = choice?.ChooserId == viewerId ? targets : [],
            ChoosingPlayerName = choice is not null && choice.ChooserId != viewerId
                ? players.FirstOrDefault(p => p.Id == choice.ChooserId)?.Name
                : null,
            Flip7PlayerId = round?.Flip7PlayerId,
            WinnerId = room.Winner,
            Log = [.. room.Log.Entries],
        };
    }
}
