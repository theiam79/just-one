using Party.Core;

namespace Party.Flip7;

/// <summary>
/// The full rules state machine for one Flip 7 room. Purely synchronous and not thread-safe:
/// the web layer serializes access behind a lock and handles change notification.
/// </summary>
/// <remarks>
/// Flip 7 is a turn game, unlike Just One where every phase is simultaneous. Only one player
/// is ever being asked for anything, so the room drives itself forward through <see cref="Pump"/>:
/// after every input it advances — dealing, flipping, resolving action cards — until it genuinely
/// needs a human again, or the round ends.
/// </remarks>
public sealed class Flip7Room : RoomBase
{
    public const int MinPlayers = 3;

    /// <summary>
    /// The published rules say "3+" with no cap. This is a product decision: past this the deal
    /// order alone is hundreds of turns, and each player waits out the whole table between goes.
    /// </summary>
    public const int MaxPlayers = 20;

    /// <summary>Off by default. When set, each connected player's turn auto-stays after this long.</summary>
    public const int MinTurnTimerSeconds = 5;
    public const int MaxTurnTimerSeconds = 120;

    private readonly Func<int, IReadOnlyList<Card>> _deckFactory;
    private readonly Random _rng;
    private readonly Func<DateTimeOffset> _now;
    private readonly Queue<Card> _deck = new();
    private readonly List<Card> _discard = [];
    private readonly Dictionary<Guid, int> _totals = [];

    /// <summary>Each finished round's points by player, oldest round first — the scorecard's data.</summary>
    private readonly List<Dictionary<Guid, int>> _roundScores = [];

    /// <summary>Decisions the round is blocked on, oldest first.</summary>
    private readonly Queue<PendingChoice> _choices = new();

    /// <summary>
    /// Action cards that have been given a target but have not taken effect yet. Nothing here is
    /// resolved while <see cref="_choices"/> is non-empty, which is what makes a Flip Three's
    /// set-aside cards all get assigned before any of them fires.
    /// </summary>
    private readonly Queue<(Card Card, Guid TargetId, Guid ChooserId)> _toResolve = new();

    private FlipThreeState? _flipThree;
    private int _dealIndex;
    private int _turnIndex;

    /// <summary>
    /// Who deals. Held as an identity rather than a seat number because the roster shifts
    /// between rounds — someone leaves, someone is sat out — and an index would quietly come
    /// to mean a different player.
    /// </summary>
    private Guid? _dealerId;

    /// <summary>
    /// The current player has had their go, but the turn can't move on yet. A card they turned
    /// up may still be looking for a target, and where it lands can decide who plays next —
    /// so the turn advances only once everything it set off has finished resolving.
    /// </summary>
    private bool _turnSpent;

    public Flip7Room(string code, Func<int, IReadOnlyList<Card>> deckFactory, Random rng, Func<DateTimeOffset>? clock = null)
        : base(code)
    {
        _deckFactory = deckFactory;
        _rng = rng;
        _now = clock ?? (() => DateTimeOffset.UtcNow);
    }

    protected override int MinSeats => MinPlayers;

    protected override int MaxSeats => MaxPlayers;

    protected override bool DealsInNewPlayers => Phase is Flip7Phase.Lobby;

    protected override bool SeatsAreFree => Phase is Flip7Phase.Lobby or Flip7Phase.GameOver;

    /// <summary>They banked what they had; the turn can move past them.</summary>
    protected override void OnPlayerSidelined(Guid id) => SidelineFromRound(id);

    /// <summary>
    /// Nobody else can act until the current player does, so a dropped circuit would stall the
    /// whole table. Drive it forward rather than waiting on someone who has gone.
    /// </summary>
    protected override void OnConnectionChanged(Guid id) => Pump();

    /// <summary>A room that plays with real, shuffled decks.</summary>
    public static Flip7Room Standard(string code, Random rng) => new(code, Flip7Deck.Shuffled(rng), rng);

    public Flip7Phase Phase { get; private set; } = Flip7Phase.Lobby;
    public RoundState? Round { get; private set; }
    public int DeckCount => _deck.Count;
    public int DiscardCount => _discard.Count;

    /// <summary>How long each turn gets before it auto-stays, or 0 for no timer. A lobby setting.</summary>
    public int TurnTimerSeconds { get; private set; }

    /// <summary>Running totals across the game's rounds.</summary>
    public IReadOnlyDictionary<Guid, int> Totals => _totals;

    /// <summary>Per-round points by player, one entry per finished round, oldest first.</summary>
    public IReadOnlyList<IReadOnlyDictionary<Guid, int>> RoundScores => _roundScores;

    /// <summary>The decision the round is waiting on, if any. Nothing else can happen until it's answered.</summary>
    public PendingChoice? PendingChoice => _choices.Count > 0 ? _choices.Peek() : null;

    /// <summary>
    /// Who the <see cref="PendingChoice"/> may be played on, in seat order. Empty when nothing
    /// is pending.
    /// </summary>
    public IReadOnlyList<Guid> LegalChoiceTargets =>
        PendingChoice is { } choice ? LegalTargets(choice) : [];

    /// <summary>
    /// Who has won, or null if nobody has yet. Only meaningful at the end of a round: somebody
    /// must be at or past the target, and they must be alone at the top — a tie means everyone
    /// plays on.
    /// </summary>
    public Guid? Winner
    {
        get
        {
            if (_totals.Count == 0)
            {
                return null;
            }

            var best = _totals.Values.Max();
            if (best < Flip7Rules.WinningScore)
            {
                return null;
            }

            var leaders = _totals.Where(t => t.Value == best).Select(t => t.Key).ToList();
            return leaders.Count == 1 ? leaders[0] : null;
        }
    }

    // ---- Game flow ----

    /// <summary>
    /// Sets the per-turn timer, or 0 to turn it off. A lobby setting so it can't change under the
    /// table mid-round. When on, a turn that runs out auto-stays the player (or takes a card if
    /// they've nothing to stay on yet) — the same thing the engine already does for someone away.
    /// </summary>
    public void SetTurnTimer(Guid callerId, int seconds)
    {
        RequirePhase(Flip7Phase.Lobby);
        RequireHostPowers(callerId);
        if (seconds != 0 && seconds is < MinTurnTimerSeconds or > MaxTurnTimerSeconds)
        {
            throw new GameRuleException($"Pick a turn timer between {MinTurnTimerSeconds} and {MaxTurnTimerSeconds} seconds, or 0 for none.");
        }

        TurnTimerSeconds = seconds;
    }

    /// <summary>
    /// Reports that the current turn's timer has run out. Self-checking and idempotent: it acts
    /// only if that exact player is still on the clock past that exact deadline, so an early or
    /// stale fire — a client racing a turn that has already moved on — does nothing. The deadline
    /// match plus the server-side clock check are what stop one client cutting another's turn short.
    /// </summary>
    public void TurnTimeUp(Guid playerId, DateTimeOffset deadline)
    {
        if (Phase is not Flip7Phase.Turns || _choices.Count > 0)
        {
            return;
        }

        if (Round?.CurrentPlayerId != playerId || Round.TurnDeadline != deadline || _now() < deadline)
        {
            return;
        }

        Round.TurnDeadline = null;

        // Exactly what an absent player gets: stay on a line worth keeping, otherwise take a card.
        AutoPlay(playerId);
        Pump();
    }

    public void StartGame(Guid callerId)
    {
        RequirePhase(Flip7Phase.Lobby);
        RequireHostPowers(callerId);
        RequireEnoughPlayers();
        ClearSpectators();

        var seats = Seats();
        _deck.Clear();
        _discard.Clear();
        foreach (var card in _deckFactory(seats.Count))
        {
            _deck.Enqueue(card);
        }

        _totals.Clear();
        _roundScores.Clear();
        foreach (var id in seats)
        {
            _totals[id] = 0;
        }

        _dealerId = seats[0];
        StartRound(1);
    }

    /// <summary>Hit: take the next card.</summary>
    public void Hit(Guid callerId)
    {
        RequireTurn(callerId);
        var card = Draw();
        if (card is null)
        {
            ExhaustRound();
            return;
        }

        Give(callerId, card);
        _turnSpent = true;
        Pump();
    }

    /// <summary>Stay: stop here and bank the line.</summary>
    public void Stay(Guid callerId)
    {
        RequireTurn(callerId);
        var hand = Round![callerId];
        if (hand.Tableau.IsEmpty)
        {
            // "You may Stay as long as you have a card in front of you."
            throw new GameRuleException("You need a card in front of you before you can stay.");
        }

        hand.Status = RoundStatus.Stayed;
        Narrate($"{Name(callerId)} stays.", Flip7LogKind.Stay);
        _turnSpent = true;
        Pump();
    }

    /// <summary>Answers whatever <see cref="PendingChoice"/> is blocking the round.</summary>
    public void ChooseTarget(Guid callerId, Guid targetId)
    {
        if (Phase is not (Flip7Phase.Dealing or Flip7Phase.Turns))
        {
            throw new GameRuleException("That move isn't available right now.");
        }

        var choice = PendingChoice ?? throw new GameRuleException("Nothing needs a target right now.");
        if (choice.ChooserId != callerId)
        {
            throw new GameRuleException("That's not your card to play.");
        }

        if (!LegalTargets(choice).Contains(targetId))
        {
            throw new GameRuleException("You can't play that card on them.");
        }

        Apply(_choices.Dequeue(), targetId);
        Pump();
    }

    public void NextRound(Guid callerId)
    {
        RequirePhase(Flip7Phase.RoundResult);
        RequireHostPowers(callerId);

        if (Winner is not null)
        {
            Phase = Flip7Phase.GameOver;
            return;
        }

        _dealerId = NextDealer();
        StartRound(Round!.RoundNumber + 1);
    }

    public void PlayAgain(Guid callerId)
    {
        RequirePhase(Flip7Phase.GameOver);
        RequireHostPowers(callerId);
        Phase = Flip7Phase.Lobby;
        Round = null;
        _totals.Clear();
        _roundScores.Clear();
        _deck.Clear();
        _discard.Clear();
        ClearSpectators();
    }

    // ---- The pump ----

    /// <summary>
    /// Drives the round forward until it needs a human — someone to answer a pending choice or
    /// to Hit or Stay — or until the round is over. Everything the rules do "automatically"
    /// happens here.
    /// </summary>
    private void Pump()
    {
        if (Phase is not (Flip7Phase.Dealing or Flip7Phase.Turns))
        {
            return;
        }

        while (true)
        {
            // A Flip 7 ends the round the instant it happens, wherever we are.
            if (Round!.Flip7PlayerId is not null)
            {
                EndRound();
                return;
            }

            if (_choices.Count > 0)
            {
                var head = _choices.Peek();

                // Everyone it could have been played on may have gone since it was offered —
                // left the room, or been sat out. With nobody to name, it can't be played and
                // its owner would otherwise be stuck holding it, unable to answer or move on.
                if (LegalTargets(head).Count == 0)
                {
                    _discard.Add(_choices.Dequeue().Card);
                    continue;
                }

                // Nobody else can act until this is answered, so a dropped player would stall
                // the whole table. Decide for them rather than freezing the round.
                if (!IsConnected(head.ChooserId))
                {
                    var choice = _choices.Dequeue();
                    Apply(choice, AutoTarget(choice));
                    continue;
                }

                return;
            }

            if (_flipThree is not null)
            {
                StepFlipThree();
                continue;
            }

            if (_toResolve.Count > 0)
            {
                var (card, target, chooser) = _toResolve.Dequeue();
                Resolve(card, target, chooser);
                continue;
            }

            if (Phase is Flip7Phase.Dealing)
            {
                if (_dealIndex < Round.Order.Count)
                {
                    var id = Round.Order[_dealIndex++];

                    // An action card dealt earlier can put someone out before the deal reaches
                    // them — they get no card at all.
                    if (!Round[id].IsActive)
                    {
                        continue;
                    }

                    var card = Draw();
                    if (card is null)
                    {
                        ExhaustRound();
                        return;
                    }

                    Give(id, card);
                    continue;
                }

                Phase = Flip7Phase.Turns;
                _turnIndex = -1;
                _turnSpent = true;
                continue;
            }

            // Everything the last go set in motion has finished, so the turn can move on.
            if (_turnSpent)
            {
                _turnSpent = false;
                AdvanceTurn();
                continue;
            }

            if (Round.CurrentPlayerId is not { } current)
            {
                EndRound();
                return;
            }

            // Their own card may have put them out — a Freeze played on themselves, say.
            if (!Round[current].IsActive)
            {
                AdvanceTurn();
                continue;
            }

            if (!IsConnected(current))
            {
                AutoPlay(current);
                continue;
            }

            return;
        }
    }

    /// <summary>
    /// Plays for someone who isn't here: stay where that's allowed, hit where it isn't.
    /// </summary>
    /// <remarks>
    /// Staying costs them nothing they already had — it banks the line and can't bust. Hitting
    /// is only forced on a player holding nothing, and a first card can't bust either, so the
    /// common case is safe. It is not risk-free in every case: an empty-handed player is forced
    /// to hit, and if that card is a Flip Three then <see cref="AutoTarget"/> aims it at them and
    /// the flips can bust them. There is no safe alternative — the card has to be played on an
    /// active player, and picking someone else would punish a third party for this one's dropped
    /// connection.
    /// </remarks>
    private void AutoPlay(Guid id)
    {
        var hand = Round![id];
        if (!hand.Tableau.IsEmpty)
        {
            hand.Status = RoundStatus.Stayed;
            Narrate($"{Name(id)} stays (away).", Flip7LogKind.Stay);
            _turnSpent = true;
            return;
        }

        var card = Draw();
        if (card is null)
        {
            ExhaustRound();
            return;
        }

        Give(id, card);
        _turnSpent = true;
    }

    /// <summary>
    /// Who an absent player's action card lands on. Themselves where that's legal — the choice
    /// that can't be accused of picking on anyone, even though a Flip Three aimed that way can
    /// bust them — otherwise the first seat that can take it.
    /// </summary>
    private Guid AutoTarget(PendingChoice choice)
    {
        var legal = LegalTargets(choice);
        return legal.Contains(choice.ChooserId) ? choice.ChooserId : legal[0];
    }

    private void StepFlipThree()
    {
        var flip = _flipThree!;
        var hand = Round![flip.TargetId];

        // "Stop if the player has Flip 7 Number cards, or the player busts" — the cards set
        // aside during the flips are then discarded without ever taking effect.
        var stopped = !hand.IsActive || Round.Flip7PlayerId is not null;
        if (stopped || flip.Remaining == 0)
        {
            if (stopped)
            {
                _discard.AddRange(flip.SetAside);
            }
            else
            {
                // Assign every set-aside card before any of them fires, oldest flip first.
                foreach (var card in flip.SetAside)
                {
                    Offer(flip.TargetId, card, ChoiceKind.ActionTarget);
                }
            }

            _flipThree = null;
            return;
        }

        var next = Draw();
        if (next is null)
        {
            _discard.AddRange(flip.SetAside);
            _flipThree = null;
            ExhaustRound();
            return;
        }

        flip.Remaining--;
        Give(flip.TargetId, next);
    }

    // ---- Cards ----

    /// <summary>Hands one card to a player and applies whatever it does.</summary>
    private void Give(Guid id, Card card)
    {
        var hand = Round![id];
        Narrate($"{Name(id)} drew {CardWord(card)}.", Flip7LogKind.Draw);

        switch (card)
        {
            case NumberCard number when Flip7Rules.WouldBust(hand.Tableau, card):
                if (hand.Tableau.SecondChance is { } saved)
                {
                    // The Second Chance is spent but stays face up (translucent) until the round
                    // ends, so the table remembers it saved a bust; only the duplicate leaves
                    // play. The turn ends there — no replacement card until their next go — and
                    // they stay active.
                    hand.Tableau.Spend(saved);
                    _discard.Add(number);
                    Narrate($"{Name(id)}'s Second Chance cancels the second {number.Value}.", Flip7LogKind.SecondChance);
                    return;
                }

                hand.Tableau.Add(number);
                hand.Status = RoundStatus.Busted;
                Narrate($"{Name(id)} busts on a second {number.Value}.", Flip7LogKind.Bust);
                return;

            case NumberCard number:
                hand.Tableau.Add(number);
                if (Flip7Rules.IsFlip7(hand.Tableau))
                {
                    Round.Flip7PlayerId = id;
                    Narrate($"{Name(id)} hits Flip 7!", Flip7LogKind.Flip7);
                }

                return;

            case ModifierCard modifier:
                hand.Tableau.Add(modifier);
                return;

            case ActionCard action:
                GiveAction(id, action);
                return;
        }
    }

    private void GiveAction(Guid id, ActionCard action)
    {
        var hand = Round![id];

        if (action.Kind is ActionKind.SecondChance)
        {
            // Revealed during a Flip Three it still resolves right away, unlike the other two.
            if (!hand.Tableau.HasSecondChance)
            {
                hand.Tableau.Add(action);
                return;
            }

            // You may only hold one, so a second has to go to someone who hasn't got one.
            if (SecondChanceRecipients(id).Count == 0)
            {
                _discard.Add(action);
                Narrate($"{Name(id)} already holds a Second Chance — the extra is discarded.", Flip7LogKind.Info);
                return;
            }

            Offer(id, action, ChoiceKind.SecondChanceRecipient);
            return;
        }

        // A Freeze or Flip Three turned up mid-flip waits until all three cards are down.
        if (_flipThree is not null && _flipThree.TargetId == id)
        {
            _flipThree.SetAside.Add(action);
            return;
        }

        Offer(id, action, ChoiceKind.ActionTarget);
    }

    /// <summary>Asks for a target, or picks the only one there is.</summary>
    private void Offer(Guid chooserId, Card card, ChoiceKind kind)
    {
        var choice = new PendingChoice(chooserId, card, kind);
        var legal = LegalTargets(choice);

        if (legal.Count == 0)
        {
            _discard.Add(card);
            return;
        }

        if (legal.Count == 1)
        {
            // "If you are the only active player in the round, you must play the Action card on
            // yourself." Nothing to decide, so don't stop the round to ask.
            Apply(choice, legal[0]);
            return;
        }

        _choices.Enqueue(choice);
    }

    private void Apply(PendingChoice choice, Guid targetId)
    {
        if (choice.Kind is ChoiceKind.SecondChanceRecipient)
        {
            Round![targetId].Tableau.Add(choice.Card);
            Narrate($"{Name(choice.ChooserId)} gives {Name(targetId)} a Second Chance.", Flip7LogKind.SecondChance);
            return;
        }

        _toResolve.Enqueue((choice.Card, targetId, choice.ChooserId));
    }

    private void Resolve(Card card, Guid targetId, Guid chooserId)
    {
        var hand = Round![targetId];

        // The target may have gone out between being handed the card and it taking effect —
        // frozen by an earlier card from the same Flip Three, or busted by one. It just goes.
        if (!hand.IsActive)
        {
            _discard.Add(card);
            return;
        }

        switch (((ActionCard)card).Kind)
        {
            case ActionKind.Freeze:
                // The Freeze stays face up in front of the player it stopped until the round
                // ends, so it isn't available to a mid-round reshuffle.
                hand.Tableau.Add(card);
                hand.Status = RoundStatus.Frozen;
                Narrate(FromTo(chooserId, targetId, "freezes"), Flip7LogKind.Freeze);
                return;

            case ActionKind.FlipThree:
                _discard.Add(card);
                _flipThree = new FlipThreeState { TargetId = targetId };
                Narrate(FromTo(chooserId, targetId, "flips three on"), Flip7LogKind.FlipThree);
                return;
        }
    }

    private List<Guid> LegalTargets(PendingChoice choice) => choice.Kind switch
    {
        ChoiceKind.SecondChanceRecipient => SecondChanceRecipients(choice.ChooserId),
        _ => Round!.ActivePlayers.ToList(),
    };

    private List<Guid> SecondChanceRecipients(Guid chooserId) =>
        [.. Round!.ActivePlayers.Where(id => id != chooserId && !Round[id].Tableau.HasSecondChance)];

    private Card? Draw()
    {
        if (_deck.Count == 0)
        {
            Reshuffle();
        }

        return _deck.Count == 0 ? null : _deck.Dequeue();
    }

    /// <summary>
    /// Rebuilds the deck from the discard pile. Only what has actually been discarded comes
    /// back: everything still in front of a player stays where it is, busted lines included.
    /// </summary>
    private void Reshuffle()
    {
        if (_discard.Count == 0)
        {
            return;
        }

        var cards = _discard.ToArray();
        _discard.Clear();
        Flip7Deck.Shuffle(cards, _rng);
        foreach (var card in cards)
        {
            _deck.Enqueue(card);
        }
    }

    // ---- Rounds ----

    private void StartRound(int number)
    {
        var seats = Seats();
        if (seats.Count == 0)
        {
            throw new GameRuleException("There's nobody left to play.");
        }

        // The dealer may have left since last round; the next seat round takes it on.
        var dealerSeat = _dealerId is { } dealer ? seats.IndexOf(dealer) : 0;
        if (dealerSeat < 0)
        {
            dealerSeat = 0;
        }

        _dealerId = seats[dealerSeat];

        // The deal starts to the dealer's left and comes back round to them last.
        var order = seats.Skip(dealerSeat + 1).Concat(seats.Take(dealerSeat + 1)).ToList();

        Round = new RoundState { RoundNumber = number, Order = order, DealerId = seats[dealerSeat] };
        foreach (var id in order)
        {
            Round.Hands[id] = new PlayerRound();
            _totals.TryAdd(id, 0);
        }

        _choices.Clear();
        _toResolve.Clear();
        _flipThree = null;
        _dealIndex = 0;
        _turnIndex = -1;
        _turnSpent = false;

        // The feed keeps every round — this line just marks where a new one begins; scroll back
        // for what came before.
        Narrate($"Round {number} — dealing.", Flip7LogKind.Info);

        Phase = Flip7Phase.Dealing;
        Pump();
    }

    private void AdvanceTurn()
    {
        var order = Round!.Order;
        for (var step = 1; step <= order.Count; step++)
        {
            var index = (_turnIndex + step) % order.Count;
            if (Round[order[index]].IsActive)
            {
                _turnIndex = index;
                Round.CurrentPlayerId = order[index];
                Round.TurnDeadline = ArmTurnTimer(order[index]);
                return;
            }
        }

        Round.CurrentPlayerId = null;
        Round.TurnDeadline = null;
    }

    /// <summary>
    /// The deadline a fresh turn gets, or null. Only armed for a player who is actually here —
    /// an away player is played at once by the pump, so a timer on them would never be read.
    /// </summary>
    private DateTimeOffset? ArmTurnTimer(Guid playerId) =>
        TurnTimerSeconds > 0 && IsConnected(playerId)
            ? _now().AddSeconds(TurnTimerSeconds)
            : null;

    /// <summary>Nothing left to deal and nothing to reshuffle: everyone still in banks what they have.</summary>
    private void ExhaustRound()
    {
        Narrate("The deck is spent — everyone still in banks their line.", Flip7LogKind.Info);
        foreach (var id in Round!.ActivePlayers.ToList())
        {
            Round[id].Status = RoundStatus.Stayed;
        }

        EndRound();
    }

    private void EndRound()
    {
        // The pump can reach here twice on the way out of a round that ended itself; scoring a
        // second time would be silently wrong the moment scoring stops being idempotent.
        if (Phase is not (Flip7Phase.Dealing or Flip7Phase.Turns))
        {
            return;
        }

        // Anything mid-flight stops where it is; those cards never take effect.
        if (_flipThree is not null)
        {
            _discard.AddRange(_flipThree.SetAside);
            _flipThree = null;
        }

        while (_choices.Count > 0)
        {
            _discard.Add(_choices.Dequeue().Card);
        }

        while (_toResolve.Count > 0)
        {
            _discard.Add(_toResolve.Dequeue().Card);
        }

        var roundScores = new Dictionary<Guid, int>();
        foreach (var id in Round!.Order)
        {
            var hand = Round[id];
            var score = Flip7Rules.Score(hand.Tableau, !hand.Scores);
            roundScores[id] = score;
            _totals[id] = _totals.GetValueOrDefault(id) + score;

            // The round's cards are set aside — they aren't shuffled back until the deck runs dry.
            // Spent cards (a used Second Chance) go back too, so the deck stays whole.
            _discard.AddRange(hand.Tableau.Cards);
            _discard.AddRange(hand.Tableau.Spent);
            hand.Tableau.Clear();
        }

        _roundScores.Add(roundScores);

        Round.CurrentPlayerId = null;
        Phase = Flip7Phase.RoundResult;
    }

    // ---- Internals ----

    private bool IsConnected(Guid id) => Players.FirstOrDefault(p => p.Id == id)?.IsConnected ?? false;

    private string Name(Guid id) => Players.FirstOrDefault(p => p.Id == id)?.Name ?? "A player";

    /// <summary>A log line for one player acting on another, reading naturally when they're the same.</summary>
    private string FromTo(Guid chooserId, Guid targetId, string verb) =>
        chooserId == targetId
            ? $"{Name(chooserId)} {verb} themselves."
            : $"{Name(chooserId)} {verb} {Name(targetId)}.";

    /// <summary>How a card reads in the log — the same vocabulary the table uses.</summary>
    private static string CardWord(Card card) => card switch
    {
        NumberCard n => n.Value.ToString(),
        ModifierCard { Kind: ModifierKind.Times2 } => "×2",
        ModifierCard m => $"+{m.PlusValue}",
        ActionCard { Kind: ActionKind.Freeze } => "a Freeze",
        ActionCard { Kind: ActionKind.FlipThree } => "a Flip Three",
        ActionCard { Kind: ActionKind.SecondChance } => "a Second Chance",
        _ => "a card",
    };

    private List<Guid> Seats() => [.. Seated.Select(p => p.Id)];

    /// <summary>
    /// The next player round from the current dealer. Walks the whole roster rather than the
    /// playing seats, so that a dealer who has left still passes it to whoever was on their
    /// left rather than throwing it back to the top of the table.
    /// </summary>
    private Guid NextDealer()
    {
        var seats = Seats();
        if (seats.Count == 0)
        {
            throw new GameRuleException("There's nobody left to play.");
        }

        var from = _dealerId is { } dealer ? Players.ToList().FindIndex(p => p.Id == dealer) : -1;
        if (from < 0)
        {
            // The dealer isn't even in the room any more; start from the top.
            return seats[0];
        }

        for (var step = 1; step <= Players.Count; step++)
        {
            var candidate = Players[(from + step) % Players.Count];
            if (!candidate.IsSpectator)
            {
                return candidate.Id;
            }
        }

        return seats[0];
    }

    /// <summary>
    /// Takes someone out of the round in progress without ending it — they left, or the host sat
    /// them out. They bank what they have, exactly as if they'd stayed.
    /// </summary>
    private void SidelineFromRound(Guid id)
    {
        if (Round is null || !Round.Hands.TryGetValue(id, out var hand) || !hand.IsActive)
        {
            return;
        }

        hand.Status = RoundStatus.Stayed;
        Narrate($"{Name(id)} is out — their line is banked.", Flip7LogKind.Info);
        while (_choices.Count > 0 && _choices.Peek().ChooserId == id)
        {
            var choice = _choices.Dequeue();
            var legal = LegalTargets(choice);
            if (legal.Count == 0)
            {
                _discard.Add(choice.Card);
            }
            else
            {
                Apply(choice, legal[0]);
            }
        }

        if (Round.CurrentPlayerId == id)
        {
            _turnSpent = true;
        }

        Pump();
    }

    private void RequirePhase(Flip7Phase phase)
    {
        if (Phase != phase)
        {
            throw new GameRuleException("That move isn't available right now.");
        }
    }

    private void RequireTurn(Guid callerId)
    {
        if (Phase is not Flip7Phase.Turns)
        {
            throw new GameRuleException("That move isn't available right now.");
        }

        if (_choices.Count > 0)
        {
            throw new GameRuleException("There's a card to play first.");
        }

        if (Round!.CurrentPlayerId != callerId)
        {
            throw new GameRuleException("It's not your turn.");
        }
    }

}
