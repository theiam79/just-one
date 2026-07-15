namespace JustOne.Core;

/// <summary>
/// The full rules state machine for one room. Purely synchronous and not thread-safe:
/// the web layer serializes access behind a lock and handles change notification.
/// </summary>
public sealed class GameRoom
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 12;
    public const int CardsPerGame = 13;
    public const int WordsPerCard = 5;
    public const int MaxNameLength = 20;
    /// <summary>At or below this many players, <see cref="TwoCluesMode.Auto"/> asks for two clues each.</summary>
    public const int TwoCluesMaxPlayers = 4;
    public const int MinTimerSeconds = 15;
    public const int MaxTimerSeconds = 300;
    public const int DefaultTimerSeconds = 60;

    private readonly string[] _words;
    private readonly Random _rng;
    private readonly List<Player> _players = [];
    private readonly Queue<Card> _deck = new();
    private int _guesserIndex;

    /// <summary>The most recent guesser, remembered across games so the rotation
    /// resumes from the right player even when seats shift between games. Null until
    /// the first round of the room's first game.</summary>
    private Guid? _lastGuesserId;

    public GameRoom(string code, IEnumerable<string> words, Random rng)
    {
        Code = code;
        _rng = rng;
        _words = words.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (_words.Length < CardsPerGame * WordsPerCard)
        {
            throw new ArgumentException($"Need at least {CardsPerGame * WordsPerCard} distinct words.", nameof(words));
        }
    }

    public string Code { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    public IReadOnlyList<Player> Players => _players;
    public RoundState? Round { get; private set; }
    public int Score { get; private set; }
    public int DeckCount => _deck.Count;
    public List<RoundRecord> CompletedRounds { get; } = [];
    public List<GameSummary> History { get; } = [];

    /// <summary>Cards not yet resolved, including the one in play.</summary>
    public int CardsLeft => _deck.Count + (Phase is GamePhase.NumberPick or GamePhase.ClueWriting or GamePhase.ClueReview or GamePhase.Guessing or GamePhase.Judging ? 1 : 0);

    public Player? Host => _players.FirstOrDefault(p => p.IsHost);

    /// <summary>How long the group's shared countdown runs for; set by the host in the lobby.</summary>
    public int TimerSeconds { get; private set; } = DefaultTimerSeconds;

    /// <summary>Whether clue-givers write two clues each; set by the host in the lobby.</summary>
    public TwoCluesMode TwoCluesMode { get; private set; } = TwoCluesMode.Auto;

    // ---- Roster ----

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

        if (_players.Count >= MaxPlayers)
        {
            throw new GameRuleException($"This room is full ({MaxPlayers} players max).");
        }

        var player = new Player
        {
            Id = id,
            Name = name,
            IsHost = _players.Count == 0,
            IsSpectator = Phase is not GamePhase.Lobby,
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

        if (Phase is GamePhase.Lobby or GamePhase.GameOver || player.IsSpectator)
        {
            _players.Remove(player);
        }
        else
        {
            // Mid-game: keep the seat (scores/history reference it) but stop expecting anything from them.
            player.IsSpectator = true;
            if (Phase == GamePhase.ClueWriting && Round is not null
                && Round.ExpectedWriters.Contains(id) && !Round.Clues.ContainsKey(id))
            {
                Round.SkippedWriters.Add(id);
                TryFinishClueWriting();
            }
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
    /// Sits an away player out until they come back, so the host doesn't have to skip them
    /// every single round: they stop being dealt guesser turns and stop being expected to
    /// write clues. The decision is sticky — it lifts while they're actually connected and
    /// re-arms if they drop again, so a flaky circuit doesn't quietly put them back in the
    /// game — and clears entirely once a new game starts with them present.
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

        // Unblock the round in progress if it was still waiting on their clue.
        if (Phase == GamePhase.ClueWriting && Round is not null
            && Round.ExpectedWriters.Contains(targetId) && !Round.Clues.ContainsKey(targetId))
        {
            Round.SkippedWriters.Add(targetId);
            TryFinishClueWriting();
        }
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
            // They're active again — back in from the next round. The bench decision itself
            // is kept so a brief reconnect doesn't discard it; it re-arms below if they drop
            // again, and ClearSpectators retires it once a new game starts with them here.
            player.IsSpectator = false;
        }
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
    }

    // ---- Settings ----

    /// <summary>
    /// Sets whether clue-givers write two clues each. A lobby setting, so the choice itself
    /// can't change mid-game — though on <see cref="TwoCluesMode.Auto"/> the rule still follows
    /// the table, so a round that starts short-handed asks for two even if the last asked for one.
    /// </summary>
    public void SetTwoCluesMode(Guid callerId, TwoCluesMode mode)
    {
        RequirePhase(GamePhase.Lobby);
        RequireHostPowers(callerId);
        TwoCluesMode = mode;
    }

    /// <summary>How many clues each writer owes, given how many are actually playing.</summary>
    private int CluesPerWriterFor(int activePlayers) => TwoCluesMode switch
    {
        TwoCluesMode.Always => 2,
        TwoCluesMode.Never => 1,
        _ => activePlayers <= TwoCluesMaxPlayers ? 2 : 1,
    };

    /// <summary>Sets how long the shared countdown runs for. A lobby setting, so it can't
    /// change under the group mid-round.</summary>
    public void SetTimerSeconds(Guid callerId, int seconds)
    {
        RequirePhase(GamePhase.Lobby);
        RequireHostPowers(callerId);
        if (seconds is < MinTimerSeconds or > MaxTimerSeconds)
        {
            throw new GameRuleException($"Pick a timer between {MinTimerSeconds} and {MaxTimerSeconds} seconds.");
        }

        TimerSeconds = seconds;
    }

    /// <summary>
    /// Starts the group's shared countdown for the phase in progress. Any player can start it
    /// (and starting again restarts it) — it's a nudge to keep things moving, not a rule:
    /// nothing happens automatically when it runs out, so nobody gets cut off.
    /// </summary>
    public void StartTimer(Guid callerId)
    {
        RequirePlayer(callerId);
        if (Phase is not (GamePhase.ClueWriting or GamePhase.Guessing))
        {
            throw new GameRuleException("There's nothing to put a timer on right now.");
        }

        Round!.TimerPhase = Phase;
        Round.TimerDeadline = DateTimeOffset.UtcNow.AddSeconds(TimerSeconds);
    }

    /// <summary>Clears the shared countdown early.</summary>
    public void CancelTimer(Guid callerId)
    {
        RequirePlayer(callerId);
        if (Round is null)
        {
            return;
        }

        Round.TimerPhase = null;
        Round.TimerDeadline = null;
    }

    // ---- Game flow ----

    public void StartGame(Guid callerId)
    {
        RequirePhase(GamePhase.Lobby);
        RequireHostPowers(callerId);

        // Players benched while away sit the next game out, so they don't count towards
        // the minimum — otherwise a "full" room could start a game nobody can actually play.
        if (_players.Count(p => !StaysBenched(p)) < MinPlayers)
        {
            throw new GameRuleException($"Need at least {MinPlayers} players to start.");
        }

        ClearSpectators();

        BuildDeck();
        Score = 0;
        CompletedRounds.Clear();

        // Continue the guesser rotation across games instead of always restarting
        // at the host. The very first game of a room starts with the host (seat 0);
        // each subsequent game resumes from whoever guessed last — looked up by
        // identity so it stays correct even if seats shifted when players left the
        // lobby between games. Either way we rotate onto the seat rather than assigning
        // it directly, so a benched or absent player is never dealt round 1.
        var fromSeat = _players.Count - 1; // rotating from the last seat lands on seat 0
        if (_lastGuesserId is { } lastId)
        {
            var lastSeat = _players.FindIndex(p => p.Id == lastId);
            fromSeat = lastSeat < 0 ? Math.Min(_guesserIndex, _players.Count - 1) : lastSeat;
        }

        RotateGuesserFrom(fromSeat);
        StartRound(1);
    }

    public void PickNumber(Guid callerId, int number)
    {
        RequirePhase(GamePhase.NumberPick);
        RequireGuesser(callerId);
        if (number is < 1 or > WordsPerCard)
        {
            throw new GameRuleException($"Pick a number from 1 to {WordsPerCard}.");
        }

        Round!.ChosenNumber = number;
        Round.MysteryWord = Round.Card.Words[number - 1];
        foreach (var p in _players)
        {
            if (!p.IsSpectator && p.Id != callerId)
            {
                Round.ExpectedWriters.Add(p.Id);
            }
        }

        // Fixed here, next to ExpectedWriters, so a roster change mid-round can't move the
        // goalposts for someone already writing.
        Round.CluesPerWriter = CluesPerWriterFor(_players.Count(p => !p.IsSpectator));

        if (Round.ExpectedWriters.Count == 0)
        {
            Resolve(RoundOutcome.Passed);
            return;
        }

        Phase = GamePhase.ClueWriting;
    }

    /// <summary>Submits a writer's single clue. Only valid when one clue is owed this round.</summary>
    public void SubmitClue(Guid callerId, string text) => SubmitClues(callerId, text);

    /// <summary>
    /// Submits all of a writer's clues for the round at once — one normally, two when playing
    /// the small-group variant. All-at-once keeps a writer either done or not done, so the rest
    /// of the round (finishing, skipping, taking back) needs no notion of a half-finished writer.
    /// Re-submitting replaces the previous set.
    /// </summary>
    public void SubmitClues(Guid callerId, params string[] texts)
    {
        RequirePhase(GamePhase.ClueWriting);
        if (!Round!.ExpectedWriters.Contains(callerId))
        {
            throw new GameRuleException("You're not writing a clue this round.");
        }

        var required = Round.CluesPerWriter;
        var cleaned = texts.Select(ClueNormalizer.Clean).Where(t => t.Length > 0).ToList();
        if (cleaned.Count < required)
        {
            throw new GameRuleException(required == 1
                ? "Enter a clue first."
                : $"Write all {required} of your clues.");
        }

        if (cleaned.Count > required)
        {
            throw new GameRuleException(required == 1
                ? "Just one clue this round."
                : $"Only {required} clues this round.");
        }

        // With more than one box on screen, say which one is the problem.
        for (var i = 0; i < cleaned.Count; i++)
        {
            var error = ClueNormalizer.ValidateWord(cleaned[i]);
            if (error is not null)
            {
                throw new GameRuleException(required > 1 ? $"Clue {i + 1}: {error}" : error);
            }
        }

        var normalized = cleaned.Select(ClueNormalizer.Normalize).ToList();
        var mysteryAt = normalized.IndexOf(ClueNormalizer.Normalize(Round.MysteryWord!));
        if (mysteryAt >= 0)
        {
            throw new GameRuleException(required > 1
                ? $"Clue {mysteryAt + 1} can't be the mystery word itself."
                : "Your clue can't be the mystery word itself.");
        }

        if (normalized.Distinct().Count() != normalized.Count)
        {
            throw new GameRuleException("Your clues need to be different from each other.");
        }

        Round.SkippedWriters.Remove(callerId);
        Round.Clues[callerId] = [.. cleaned.Zip(normalized,
            (text, norm) => new Clue { AuthorId = callerId, Text = text, Normalized = norm })];
        TryFinishClueWriting();
    }

    /// <summary>
    /// Takes back the caller's already-submitted clue so they can rework it without being
    /// cut off when everyone else finishes. Only valid while clues are still being written;
    /// deliberately does not advance the phase (removing a clue can only make the round less
    /// complete), so the writing phase stays open until they submit again or are skipped.
    /// </summary>
    public void UnsubmitClue(Guid callerId)
    {
        RequirePhase(GamePhase.ClueWriting);
        if (!Round!.ExpectedWriters.Contains(callerId))
        {
            throw new GameRuleException("You're not writing a clue this round.");
        }

        if (!Round.Clues.Remove(callerId))
        {
            throw new GameRuleException("You haven't submitted a clue to take back.");
        }
    }

    public void SkipPlayerClue(Guid callerId, Guid targetId)
    {
        RequirePhase(GamePhase.ClueWriting);
        RequireHostPowers(callerId);
        if (!Round!.ExpectedWriters.Contains(targetId) || Round.Clues.ContainsKey(targetId))
        {
            throw new GameRuleException("That player isn't waiting to write a clue.");
        }

        Round.SkippedWriters.Add(targetId);
        TryFinishClueWriting();
    }

    /// <summary>
    /// Flips one clue's manual cancellation. <paramref name="clueIndex"/> picks which of the
    /// author's clues when the two-clue variant is in play; it defaults to their only one.
    /// </summary>
    public void ToggleClueCancellation(Guid callerId, Guid authorId, int clueIndex = 0)
    {
        RequirePhase(GamePhase.ClueReview);
        RequireClueReviewer(callerId);
        if (!Round!.Clues.TryGetValue(authorId, out var clues)
            || clueIndex < 0 || clueIndex >= clues.Count)
        {
            throw new GameRuleException("There's no clue from that player.");
        }

        var clue = clues[clueIndex];
        if (clue.AutoCancelled)
        {
            throw new GameRuleException("Identical clues stay cancelled — that's the rule.");
        }

        clue.ManuallyCancelled = !clue.ManuallyCancelled;
    }

    public void RevealClues(Guid callerId)
    {
        RequirePhase(GamePhase.ClueReview);
        RequireClueReviewer(callerId);
        Phase = GamePhase.Guessing;
    }

    public void SubmitGuess(Guid callerId, string guess)
    {
        RequirePhase(GamePhase.Guessing);
        RequireGuesser(callerId);
        var trimmed = guess.Trim();
        if (trimmed.Length == 0)
        {
            throw new GameRuleException("Type a guess, or pass.");
        }

        Round!.Guess = trimmed;
        if (ClueNormalizer.Normalize(trimmed) == ClueNormalizer.Normalize(Round.MysteryWord!))
        {
            Resolve(RoundOutcome.Correct);
        }
        else
        {
            // Not letter-perfect: the clue-writers decide if it's close enough (typos, plurals…).
            Phase = GamePhase.Judging;
        }
    }

    public void Pass(Guid callerId)
    {
        RequirePhase(GamePhase.Guessing);
        RequireGuesser(callerId);
        Resolve(RoundOutcome.Passed);
    }

    public void JudgeGuess(Guid callerId, bool accept)
    {
        RequirePhase(GamePhase.Judging);
        RequireClueReviewer(callerId);
        Resolve(accept ? RoundOutcome.Correct : RoundOutcome.Wrong);
    }

    /// <summary>Abandons the current round as a pass — the escape hatch when someone vanishes mid-round.</summary>
    public void SkipRound(Guid callerId)
    {
        if (Phase is not (GamePhase.NumberPick or GamePhase.ClueWriting or GamePhase.ClueReview or GamePhase.Guessing or GamePhase.Judging))
        {
            throw new GameRuleException("There's no round to skip right now.");
        }

        RequireHostPowers(callerId);
        Resolve(RoundOutcome.Passed);
    }

    public void NextRound(Guid callerId)
    {
        RequirePhase(GamePhase.RoundResult);
        RequireHostPowers(callerId);
        if (_deck.Count == 0)
        {
            Phase = GamePhase.GameOver;
            History.Add(new GameSummary(Score, CardsPerGame, Rating.For(Score), DateTimeOffset.UtcNow));
            return;
        }

        AdvanceGuesser();
        StartRound(Round!.RoundNumber + 1);
    }

    public void PlayAgain(Guid callerId)
    {
        RequirePhase(GamePhase.GameOver);
        RequireHostPowers(callerId);
        Phase = GamePhase.Lobby;
        Round = null;
        Score = 0;
        CompletedRounds.Clear();
        _deck.Clear();
        ClearSpectators();
    }

    // ---- Internals ----

    /// <summary>Whether a new game would leave this player sitting out: benched, and still away.</summary>
    private static bool StaysBenched(Player p) => p.BenchedForInactivity && !p.IsConnected;

    /// <summary>
    /// Brings spectators in as players for a new game — except anyone benched for inactivity
    /// who is still away, who stays out until they actually come back. Players who are back
    /// have their bench retired, so the next game is a clean slate for them.
    /// </summary>
    private void ClearSpectators()
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

    private void BuildDeck()
    {
        var pool = (string[])_words.Clone();
        for (var i = pool.Length - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        _deck.Clear();
        for (var c = 0; c < CardsPerGame; c++)
        {
            _deck.Enqueue(new Card([.. pool.Skip(c * WordsPerCard).Take(WordsPerCard)]));
        }
    }

    private void StartRound(int roundNumber)
    {
        Round = new RoundState
        {
            RoundNumber = roundNumber,
            GuesserId = _players[_guesserIndex].Id,
            Card = _deck.Dequeue(),
        };
        _lastGuesserId = Round.GuesserId;
        Phase = GamePhase.NumberPick;
    }

    private void AdvanceGuesser()
    {
        var currentIndex = _players.FindIndex(p => p.Id == Round!.GuesserId);
        if (currentIndex < 0)
        {
            currentIndex = Math.Min(_guesserIndex, _players.Count - 1);
        }

        RotateGuesserFrom(currentIndex);
    }

    /// <summary>
    /// Moves <see cref="_guesserIndex"/> to the next eligible seat after
    /// <paramref name="currentIndex"/>, skipping spectators and preferring connected players.
    /// Falls back to plain seat rotation when nobody is connected.
    /// </summary>
    private void RotateGuesserFrom(int currentIndex)
    {
        int? firstEligible = null;
        for (var step = 1; step <= _players.Count; step++)
        {
            var index = (currentIndex + step) % _players.Count;
            var candidate = _players[index];
            if (candidate.IsSpectator)
            {
                continue;
            }

            firstEligible ??= index;
            if (candidate.IsConnected)
            {
                _guesserIndex = index;
                return;
            }
        }

        // Nobody is connected (engine tests, or everyone dropped): plain seat rotation.
        _guesserIndex = firstEligible ?? currentIndex;
    }

    private void TryFinishClueWriting()
    {
        if (Round!.ExpectedWriters.Any(id => !Round.Clues.ContainsKey(id) && !Round.SkippedWriters.Contains(id)))
        {
            return;
        }

        ApplyAutoCancellation();
        Phase = GamePhase.ClueReview;
    }

    private void ApplyAutoCancellation()
    {
        var mystery = ClueNormalizer.Normalize(Round!.MysteryWord!);
        // Across every clue in the round, not per author: with the two-clue variant a writer's
        // clue can just as easily collide with the other writer's as with anyone else's.
        foreach (var group in Round.Clues.Values.SelectMany(c => c).GroupBy(c => c.Normalized))
        {
            if (group.Count() > 1 || group.Key == mystery)
            {
                foreach (var clue in group)
                {
                    clue.AutoCancelled = true;
                }
            }
        }
    }

    private void Resolve(RoundOutcome outcome)
    {
        Round!.Outcome = outcome;
        if (outcome == RoundOutcome.Correct)
        {
            Score++;
        }
        else if (outcome == RoundOutcome.Wrong && _deck.Count > 0)
        {
            _deck.Dequeue();
        }

        var guesserName = _players.FirstOrDefault(p => p.Id == Round.GuesserId)?.Name ?? "?";
        CompletedRounds.Add(new RoundRecord(Round.RoundNumber, Round.MysteryWord, Round.Guess, outcome, guesserName));
        Phase = GamePhase.RoundResult;
    }

    private Player GetPlayer(Guid id) =>
        _players.FirstOrDefault(p => p.Id == id) ?? throw new GameRuleException("You're not in this room.");

    private void RequirePhase(GamePhase phase)
    {
        if (Phase != phase)
        {
            throw new GameRuleException("That move isn't available right now.");
        }
    }

    /// <summary>Anyone actually playing — including the guesser, unlike <see cref="RequireClueReviewer"/>.</summary>
    private void RequirePlayer(Guid callerId)
    {
        if (GetPlayer(callerId).IsSpectator)
        {
            throw new GameRuleException("Spectators can't do that — you're in next game!");
        }
    }

    private void RequireGuesser(Guid callerId)
    {
        GetPlayer(callerId);
        if (Round?.GuesserId != callerId)
        {
            throw new GameRuleException("Only the guesser can do that.");
        }
    }

    private void RequireClueReviewer(Guid callerId)
    {
        var caller = GetPlayer(callerId);
        if (caller.IsSpectator)
        {
            throw new GameRuleException("Spectators can't do that — you're in next game!");
        }

        if (Round?.GuesserId == callerId)
        {
            throw new GameRuleException("The guesser can't do that.");
        }
    }

    /// <summary>Host-only, but if the host is disconnected anyone may drive so the game never stalls.</summary>
    private void RequireHostPowers(Guid callerId)
    {
        var caller = GetPlayer(callerId);
        if (caller.IsHost)
        {
            return;
        }

        var host = Host;
        if (host is null || !host.IsConnected)
        {
            return;
        }

        throw new GameRuleException($"Only the host ({host.Name}) can do that.");
    }
}
