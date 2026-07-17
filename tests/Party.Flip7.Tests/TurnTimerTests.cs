using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

/// <summary>
/// The per-turn timer. The lobby sets it; a connected player's turn then gets a deadline; and a
/// timeout auto-stays them — but only a real timeout, checked against the room's own clock and
/// the exact turn, so no client can cut a turn short early or from the past.
/// </summary>
public class TurnTimerTests
{
    private sealed class Clock
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    }

    private static Flip7Room Lobby(Clock clock, params Card[] cards)
    {
        var deck = cards.ToList();
        deck.AddRange(Enumerable.Repeat<Card>(new NumberCard(0), 200));
        var room = new Flip7Room("TEST", _ => deck, new Random(42), () => clock.Now);
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        return room;
    }

    private static Flip7Room Timed(int seconds, Clock clock, params Card[] cards)
    {
        var room = Lobby(clock, cards);
        room.SetTurnTimer(Alice, seconds);
        room.StartGame(Alice);   // Alice deals; the deal runs Bob, Carol, Alice, so Bob is up first
        return room;
    }

    [Test]
    public async Task Only_the_host_can_set_it_and_only_in_the_lobby()
    {
        var clock = new Clock();

        var notHost = Lobby(clock);
        ExpectRuleError(() => notHost.SetTurnTimer(Bob, 30));

        var started = Timed(30, new Clock());
        ExpectRuleError(() => started.SetTurnTimer(Alice, 45));   // no longer in the lobby
        await Assert.That(started.TurnTimerSeconds).IsEqualTo(30);
    }

    [Test]
    public async Task It_accepts_off_and_the_allowed_range_but_rejects_the_rest()
    {
        var room = Lobby(new Clock());

        room.SetTurnTimer(Alice, 0);
        await Assert.That(room.TurnTimerSeconds).IsEqualTo(0);
        room.SetTurnTimer(Alice, Flip7Room.MinTurnTimerSeconds);
        room.SetTurnTimer(Alice, Flip7Room.MaxTurnTimerSeconds);

        ExpectRuleError(() => room.SetTurnTimer(Alice, Flip7Room.MinTurnTimerSeconds - 1));
        ExpectRuleError(() => room.SetTurnTimer(Alice, Flip7Room.MaxTurnTimerSeconds + 1));
    }

    [Test]
    public async Task A_connected_players_turn_gets_a_deadline()
    {
        var clock = new Clock();
        var room = Timed(30, clock);

        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
        await Assert.That(room.Round.TurnDeadline).IsEqualTo(clock.Now.AddSeconds(30));
    }

    [Test]
    public async Task No_timer_means_no_deadline()
    {
        var room = Timed(0, new Clock());

        await Assert.That(room.Round!.TurnDeadline).IsNull();
    }

    [Test]
    public async Task Running_out_of_time_auto_stays_a_player_who_has_a_line()
    {
        var clock = new Clock();
        var room = Timed(30, clock, Num(5), Num(4), Num(3));   // Bob holds a 5 after the deal
        var deadline = room.Round!.TurnDeadline!.Value;

        clock.Now = clock.Now.AddSeconds(31);
        room.TurnTimeUp(Bob, deadline);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Stayed);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Carol);
    }

    [Test]
    public async Task An_early_time_up_does_nothing()
    {
        var clock = new Clock();
        var room = Timed(30, clock, Num(5), Num(4), Num(3));
        var deadline = room.Round!.TurnDeadline!.Value;

        // Clock hasn't reached the deadline — a client jumping the gun must not cut the turn short.
        room.TurnTimeUp(Bob, deadline);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
        await Assert.That(room.Round.TurnDeadline).IsEqualTo(deadline);
    }

    [Test]
    public async Task A_time_up_for_a_turn_that_already_moved_on_does_nothing()
    {
        var clock = new Clock();
        var room = Timed(30, clock, Num(5), Num(4), Num(3));
        var bobDeadline = room.Round!.TurnDeadline!.Value;

        room.Stay(Bob);   // turn moves to Carol, with her own deadline
        clock.Now = clock.Now.AddSeconds(31);
        room.TurnTimeUp(Bob, bobDeadline);   // stale: Bob is no longer on the clock

        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Carol);
        await Assert.That(room.StatusOf(Carol)).IsEqualTo(RoundStatus.Active);
    }

    [Test]
    public async Task A_time_up_with_the_wrong_deadline_does_nothing()
    {
        var clock = new Clock();
        var room = Timed(30, clock, Num(5), Num(4), Num(3));

        clock.Now = clock.Now.AddSeconds(31);
        room.TurnTimeUp(Bob, clock.Now);   // not the deadline the turn was armed with

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Active);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Bob);
    }
}
