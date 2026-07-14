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

    public void PlayerConnected(Guid id)
    {
        var player = _players.FirstOrDefault(p => p.Id == id);
        if (player is not null)
        {
            player.ConnectionCount++;
        }
    }

    public void PlayerDisconnected(Guid id)
    {
        var player = _players.FirstOrDefault(p => p.Id == id);
        if (player is not null && player.ConnectionCount > 0)
        {
            player.ConnectionCount--;
        }
    }

    // ---- Game flow ----

    public void StartGame(Guid callerId)
    {
        RequirePhase(GamePhase.Lobby);
        RequireHostPowers(callerId);
        if (_players.Count < MinPlayers)
        {
            throw new GameRuleException($"Need at least {MinPlayers} players to start.");
        }

        foreach (var p in _players)
        {
            p.IsSpectator = false;
        }

        BuildDeck();
        Score = 0;
        CompletedRounds.Clear();

        // Continue the guesser rotation across games instead of always restarting
        // at the host. The very first game of a room starts with the host (seat 0);
        // each subsequent game resumes from whoever guessed last — looked up by
        // identity so it stays correct even if seats shifted when players left the
        // lobby between games — and advances to the next eligible seat.
        if (_lastGuesserId is { } lastId)
        {
            var lastSeat = _players.FindIndex(p => p.Id == lastId);
            if (lastSeat < 0)
            {
                lastSeat = Math.Min(_guesserIndex, _players.Count - 1);
            }

            RotateGuesserFrom(lastSeat);
        }
        else
        {
            _guesserIndex = 0;
        }

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

        if (Round.ExpectedWriters.Count == 0)
        {
            Resolve(RoundOutcome.Passed);
            return;
        }

        Phase = GamePhase.ClueWriting;
    }

    public void SubmitClue(Guid callerId, string text)
    {
        RequirePhase(GamePhase.ClueWriting);
        if (!Round!.ExpectedWriters.Contains(callerId))
        {
            throw new GameRuleException("You're not writing a clue this round.");
        }

        var cleaned = ClueNormalizer.Clean(text);
        var error = ClueNormalizer.ValidateWord(cleaned);
        if (error is not null)
        {
            throw new GameRuleException(error);
        }

        var normalized = ClueNormalizer.Normalize(cleaned);
        if (normalized == ClueNormalizer.Normalize(Round.MysteryWord!))
        {
            throw new GameRuleException("Your clue can't be the mystery word itself.");
        }

        Round.SkippedWriters.Remove(callerId);
        Round.Clues[callerId] = new Clue { AuthorId = callerId, Text = cleaned, Normalized = normalized };
        TryFinishClueWriting();
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

    public void ToggleClueCancellation(Guid callerId, Guid authorId)
    {
        RequirePhase(GamePhase.ClueReview);
        RequireClueReviewer(callerId);
        if (!Round!.Clues.TryGetValue(authorId, out var clue))
        {
            throw new GameRuleException("There's no clue from that player.");
        }

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
        foreach (var p in _players)
        {
            p.IsSpectator = false;
        }
    }

    // ---- Internals ----

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
        foreach (var group in Round.Clues.Values.GroupBy(c => c.Normalized))
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
