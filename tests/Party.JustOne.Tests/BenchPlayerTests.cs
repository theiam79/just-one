using Party.JustOne;
using static Party.JustOne.Tests.TestGame;

namespace Party.JustOne.Tests;

/// <summary>
/// The host can sit an away player out until they come back, instead of skipping
/// them every single round (see issue #2).
/// </summary>
public class BenchPlayerTests
{
    /// <summary>Alice (host), Bob, Carol, Dave in the lobby, all connected — enough that the
    /// room still meets the minimum once one of them is benched.</summary>
    private static GameRoom Lobby4()
    {
        var room = NewRoom();
        foreach (var (id, name) in new[] { (Alice, "Alice"), (Bob, "Bob"), (Carol, "Carol"), (Dave, "Dave") })
        {
            room.Join(id, name);
            room.PlayerConnected(id);
        }

        return room;
    }

    [Test]
    public async Task Benching_an_away_writer_unblocks_the_current_round()
    {
        var room = InClueWriting();
        room.PlayerDisconnected(Carol);
        room.SubmitClue(Bob, "alpha");

        room.BenchPlayer(Alice, Carol);

        var carol = room.Players.First(p => p.Id == Carol);
        await Assert.That(carol.IsSpectator).IsTrue();
        await Assert.That(carol.BenchedForInactivity).IsTrue();
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview); // no longer waiting on Carol
    }

    [Test]
    public async Task Benched_player_is_not_expected_to_write_in_later_rounds()
    {
        var room = InGuessing();
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);

        room.Pass(Alice);
        room.NextRound(Alice);
        room.PickNumber(room.Round!.GuesserId, 1);

        // Only the non-benched, non-guessing player owes a clue now.
        await Assert.That(room.Round.ExpectedWriters).DoesNotContain(Carol);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    public async Task Benched_player_is_skipped_as_guesser()
    {
        var room = InGuessing();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);

        room.Pass(Alice);
        room.NextRound(Alice);

        // Bob would have been next by seat order; the bench skips him.
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Carol);
    }

    [Test]
    public async Task Reconnecting_lifts_the_bench_automatically()
    {
        var room = InClueWriting();
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);

        room.PlayerConnected(Carol);

        await Assert.That(room.Players.First(p => p.Id == Carol).IsSpectator).IsFalse();
    }

    [Test]
    public async Task A_momentary_reconnect_does_not_discard_the_bench()
    {
        var room = InClueWriting();
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);

        room.PlayerConnected(Carol);    // a flaky circuit briefly reconnects…
        room.PlayerDisconnected(Carol); // …and drops again

        // Carol is away again, so the host's decision still stands — they shouldn't
        // have to sit her out a second time.
        var carol = room.Players.First(p => p.Id == Carol);
        await Assert.That(carol.IsSpectator).IsTrue();
        await Assert.That(carol.IsConnected).IsFalse();
    }

    [Test]
    public async Task Coming_back_and_staying_retires_the_bench_next_game()
    {
        var room = Started3();
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        room.PlayerConnected(Carol); // back for good
        RosterTests.PlayThroughGame(room);

        room.PlayAgain(Alice);
        room.StartGame(Alice);

        var carol = room.Players.First(p => p.Id == Carol);
        await Assert.That(carol.IsSpectator).IsFalse();
        await Assert.That(carol.BenchedForInactivity).IsFalse();
    }

    [Test]
    public async Task A_benched_player_is_never_dealt_the_first_round()
    {
        // The host goes away in the lobby and is sat out before the room's first game.
        // Seat 0 is the host, so the round-1 seat must skip them rather than softlock.
        var room = Lobby4();
        room.PlayerDisconnected(Alice);
        room.BenchPlayer(Bob, Alice); // host powers fall through to Bob

        room.StartGame(Bob);

        await Assert.That(room.Round!.GuesserId).IsNotEqualTo(Alice);
        await Assert.That(room.Players.First(p => p.Id == room.Round.GuesserId).IsSpectator).IsFalse();
    }

    [Test]
    public async Task Benched_players_do_not_count_towards_the_minimum_to_start()
    {
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob); // only Alice and Carol would actually play

        var ex = ExpectRuleError(() => room.StartGame(Alice));
        await Assert.That(ex.Message).Contains("3 players");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Lobby);
    }

    [Test]
    public async Task Bench_persists_into_the_next_game_while_they_stay_away()
    {
        var room = Lobby4();
        room.StartGame(Alice);
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        RosterTests.PlayThroughGame(room);

        room.PlayAgain(Alice);
        var carol = room.Players.First(p => p.Id == Carol);
        await Assert.That(carol.IsSpectator).IsTrue(); // still away, so still sitting out

        room.StartGame(Alice);
        await Assert.That(carol.IsSpectator).IsTrue();
        await Assert.That(room.Round!.GuesserId).IsNotEqualTo(Carol);
    }

    [Test]
    public async Task An_away_spectator_who_was_never_benched_still_joins_the_next_game()
    {
        // Guards the ClearSpectators bench check: promotion must key off the bench flag,
        // not merely off being disconnected. Dave is a plain mid-game spectator who is
        // also away, so he must still be promoted for the next game.
        var room = Started3();
        room.Join(Dave, "Dave");
        RosterTests.PlayThroughGame(room);

        var dave = room.Players.First(p => p.Id == Dave);
        await Assert.That(dave.IsSpectator).IsTrue();
        await Assert.That(dave.IsConnected).IsFalse();
        await Assert.That(dave.BenchedForInactivity).IsFalse();

        room.PlayAgain(Alice);
        await Assert.That(dave.IsSpectator).IsFalse();
    }

    [Test]
    public async Task Cannot_bench_a_connected_player()
    {
        var room = InClueWriting();
        var ex = ExpectRuleError(() => room.BenchPlayer(Alice, Carol));
        await Assert.That(ex.Message).Contains("away");
    }

    [Test]
    public async Task Cannot_bench_someone_already_sitting_out()
    {
        var room = InClueWriting();
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        var ex = ExpectRuleError(() => room.BenchPlayer(Alice, Carol));
        await Assert.That(ex.Message).Contains("already");
    }

    [Test]
    public async Task Non_host_cannot_bench_while_the_host_is_connected()
    {
        var room = InClueWriting();
        room.PlayerDisconnected(Carol);
        var ex = ExpectRuleError(() => room.BenchPlayer(Bob, Carol));
        await Assert.That(ex.Message).Contains("host");
    }

    [Test]
    public async Task Anyone_can_bench_when_the_host_is_away()
    {
        var room = InClueWriting();
        room.PlayerDisconnected(Alice); // host drops
        room.PlayerDisconnected(Carol);

        room.BenchPlayer(Bob, Carol); // host powers fall through to Bob

        await Assert.That(room.Players.First(p => p.Id == Carol).IsSpectator).IsTrue();
    }
}
