using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

/// <summary>
/// The group's shared countdown (issue #3). It is deliberately advisory: the engine
/// only records a deadline and never acts on it, so nobody is cut off when it runs out.
/// </summary>
public class TimerTests
{
    [Test]
    public async Task Timer_defaults_to_sixty_seconds()
    {
        var room = Lobby3();
        await Assert.That(room.TimerSeconds).IsEqualTo(GameRoom.DefaultTimerSeconds);
        await Assert.That(room.TimerSeconds).IsEqualTo(60);
    }

    [Test]
    public async Task Host_sets_the_timer_length_in_the_lobby()
    {
        var room = Lobby3();
        room.SetTimerSeconds(Alice, 90);
        await Assert.That(room.TimerSeconds).IsEqualTo(90);
    }

    [Test]
    [Arguments(GameRoom.MinTimerSeconds - 1)]
    [Arguments(GameRoom.MaxTimerSeconds + 1)]
    [Arguments(0)]
    public async Task Timer_length_must_be_in_range(int seconds)
    {
        var room = Lobby3();
        var ex = ExpectRuleError(() => room.SetTimerSeconds(Alice, seconds));
        await Assert.That(ex.Message).Contains("seconds");
        await Assert.That(room.TimerSeconds).IsEqualTo(GameRoom.DefaultTimerSeconds);
    }

    [Test]
    public async Task Non_host_cannot_set_the_timer_length()
    {
        var room = Lobby3();
        var ex = ExpectRuleError(() => room.SetTimerSeconds(Bob, 90));
        await Assert.That(ex.Message).Contains("host");
    }

    [Test]
    public async Task Timer_length_is_a_lobby_setting()
    {
        var room = InClueWriting();
        ExpectRuleError(() => room.SetTimerSeconds(Alice, 90));
        await Assert.That(room.TimerSeconds).IsEqualTo(GameRoom.DefaultTimerSeconds);
    }

    [Test]
    public async Task Anyone_can_start_the_timer_while_clues_are_written()
    {
        var room = InClueWriting();
        var before = DateTimeOffset.UtcNow;

        room.StartTimer(Bob); // not the host, not the guesser

        var deadline = room.Round!.TimerDeadline;
        await Assert.That(deadline).IsNotNull();
        await Assert.That(deadline!.Value).IsGreaterThanOrEqualTo(before.AddSeconds(room.TimerSeconds));
        await Assert.That(room.Round.TimerPhase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    public async Task Starting_the_timer_uses_the_length_the_host_chose()
    {
        var room = Lobby3();
        room.SetTimerSeconds(Alice, 90);
        room.StartGame(Alice);
        room.PickNumber(Alice, 1);

        var before = DateTimeOffset.UtcNow;
        room.StartTimer(Bob);

        // Bounded on both sides: a countdown that quietly used the 60s default would fail.
        var deadline = room.Round!.TimerDeadline!.Value;
        await Assert.That(deadline).IsGreaterThanOrEqualTo(before.AddSeconds(90));
        await Assert.That(deadline).IsLessThan(DateTimeOffset.UtcNow.AddSeconds(91));
    }

    [Test]
    public async Task A_finished_phases_timer_is_left_behind_not_resurrected()
    {
        // The engine never clears the deadline on a phase change — the view keys off TimerPhase
        // instead. Pin that the stamp stays put, so a stale countdown can't reappear later.
        var room = InClueWriting();
        room.StartTimer(Bob);
        room.SubmitClue(Bob, "alpha");
        room.SubmitClue(Carol, "beta");

        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
        await Assert.That(room.Round!.TimerPhase).IsEqualTo(GamePhase.ClueWriting);

        room.RevealClues(Bob);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Guessing);
        await Assert.That(room.Round.TimerPhase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    public async Task The_guesser_can_start_the_timer_while_guessing()
    {
        var room = InGuessing();
        room.StartTimer(Alice);
        await Assert.That(room.Round!.TimerDeadline).IsNotNull();
        await Assert.That(room.Round.TimerPhase).IsEqualTo(GamePhase.Guessing);
    }

    [Test]
    public async Task Starting_again_restarts_the_countdown()
    {
        var room = InClueWriting();
        room.StartTimer(Bob);
        room.Round!.TimerDeadline = DateTimeOffset.UtcNow.AddSeconds(-5); // pretend it ran out

        room.StartTimer(Carol); // anyone can put more time on the clock

        await Assert.That(room.Round.TimerDeadline!.Value).IsGreaterThan(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task Timer_can_be_cancelled()
    {
        var room = InClueWriting();
        room.StartTimer(Bob);
        room.CancelTimer(Carol);
        await Assert.That(room.Round!.TimerDeadline).IsNull();
        await Assert.That(room.Round.TimerPhase).IsNull();
    }

    [Test]
    public async Task Cannot_time_a_phase_with_nothing_to_wait_for()
    {
        var room = Started3(); // NumberPick
        var ex = ExpectRuleError(() => room.StartTimer(Alice));
        await Assert.That(ex.Message).Contains("nothing to put a timer on");
    }

    [Test]
    public async Task Spectators_cannot_start_the_timer()
    {
        var room = InClueWriting();
        room.Join(Dave, "Dave"); // joins mid-game, so spectates
        var ex = ExpectRuleError(() => room.StartTimer(Dave));
        await Assert.That(ex.Message).Contains("Spectators");
    }

    [Test]
    public async Task Spectators_cannot_cancel_the_timer()
    {
        var room = InClueWriting();
        room.StartTimer(Bob);
        room.Join(Dave, "Dave");

        var ex = ExpectRuleError(() => room.CancelTimer(Dave));
        await Assert.That(ex.Message).Contains("Spectators");
        await Assert.That(room.Round!.TimerDeadline).IsNotNull(); // still running for the players
    }

    [Test]
    public async Task Expiry_does_nothing_on_its_own()
    {
        // The whole point of a soft timer: a deadline in the past changes no game state.
        var room = InClueWriting();
        room.StartTimer(Bob);
        room.Round!.TimerDeadline = DateTimeOffset.UtcNow.AddSeconds(-5);

        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
        await Assert.That(room.Round.Clues).IsEmpty();

        // And a writer who was mid-thought can still submit after time is up.
        room.SubmitClue(Bob, "alpha");
        await Assert.That(room.Round.Clues[Bob].Text).IsEqualTo("alpha");
    }

    [Test]
    public async Task A_new_round_starts_with_no_timer()
    {
        var room = InGuessing();
        room.StartTimer(Alice);
        room.Pass(Alice);
        room.NextRound(Alice);

        await Assert.That(room.Round!.TimerDeadline).IsNull();
        await Assert.That(room.Round.TimerPhase).IsNull();
    }
}
