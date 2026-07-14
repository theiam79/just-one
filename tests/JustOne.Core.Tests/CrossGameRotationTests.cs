using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

/// <summary>
/// The starting guesser must continue rotating across games rather than
/// resetting to the host every game (see issue #4).
/// </summary>
public class CrossGameRotationTests
{
    [Test]
    public async Task Next_game_continues_the_rotation_instead_of_restarting_at_host()
    {
        var room = Started3();
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Alice); // game 1 still starts with the host

        RosterTests.PlayThroughGame(room);
        room.PlayAgain(Alice);
        room.StartGame(Alice);

        // Game 1 ran 13 rounds with 3 players and ended on Alice (seat 0);
        // game 2 picks up at the next seat rather than resetting to the host.
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Bob);
        await Assert.That(room.Round.RoundNumber).IsEqualTo(1);
    }

    [Test]
    public async Task Rotation_keeps_advancing_across_several_games()
    {
        var room = Started3();
        var firstGuessers = new List<Guid> { room.Round!.GuesserId };

        for (var game = 0; game < 2; game++)
        {
            RosterTests.PlayThroughGame(room);
            room.PlayAgain(Alice);
            room.StartGame(Alice);
            firstGuessers.Add(room.Round!.GuesserId);
        }

        await Assert.That(firstGuessers[0]).IsEqualTo(Alice);
        await Assert.That(firstGuessers[1]).IsEqualTo(Bob);
        await Assert.That(firstGuessers[2]).IsEqualTo(Carol);
    }

    [Test]
    public async Task Rotation_survives_a_player_leaving_between_games()
    {
        // Four connected players so the roster still meets the minimum after one leaves.
        var room = NewRoom();
        foreach (var (id, name) in new[] { (Alice, "Alice"), (Bob, "Bob"), (Carol, "Carol"), (Dave, "Dave") })
        {
            room.Join(id, name);
            room.PlayerConnected(id);
        }

        room.StartGame(Alice);
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Alice);

        RosterTests.PlayThroughGame(room);
        room.PlayAgain(Alice);

        // Bob leaves in the lobby before game 2; his seat is removed entirely.
        room.Leave(Bob);
        room.StartGame(Alice);

        // Game 1 ended on Alice; the rotation continues to the next present player
        // without erroring on the now-shorter roster.
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Carol);
        await Assert.That(room.Players.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Resume_follows_the_last_guesser_by_identity_when_a_lower_seat_leaves()
    {
        // Five players so a full 13-round game ends on a non-zero seat (12 % 5 == 2 => Carol),
        // and the roster still meets the minimum after one leaves.
        var eve = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var room = NewRoom();
        foreach (var (id, name) in new[] { (Alice, "Alice"), (Bob, "Bob"), (Carol, "Carol"), (Dave, "Dave"), (eve, "Eve") })
        {
            room.Join(id, name);
            room.PlayerConnected(id);
        }

        room.StartGame(Alice);
        RosterTests.PlayThroughGame(room);
        await Assert.That(room.CompletedRounds[^1].GuesserName).IsEqualTo("Carol"); // game 1 ended on Carol

        room.PlayAgain(Alice);

        // Bob (a seat *below* Carol) leaves, shifting Carol/Dave/Eve down one index.
        // A raw carried-over index would now point past Carol at Eve; tracking the last
        // guesser by identity keeps the resume anchored to Carol, so Dave guesses next.
        room.Leave(Bob);
        room.StartGame(Alice);

        await Assert.That(room.Round!.GuesserId).IsEqualTo(Dave);
    }
}
