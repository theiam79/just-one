using Party.JustOne;
using static Party.JustOne.Tests.TestGame;

namespace Party.JustOne.Tests;

public class GameEndTests
{
    [Test]
    public async Task Game_ends_when_the_deck_is_exhausted()
    {
        var room = Started3();
        RosterTests.PlayThroughGame(room);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.GameOver);
        await Assert.That(room.CompletedRounds.Count).IsEqualTo(GameRoom.CardsPerGame);
        await Assert.That(room.History.Count).IsEqualTo(1);
        await Assert.That(room.History[0].Score).IsEqualTo(0);
        await Assert.That(room.History[0].Rating).IsEqualTo("Try again!");
    }

    [Test]
    public async Task Wrong_guesses_shorten_the_game()
    {
        var room = Started3();
        var rounds = 0;
        while (room.Phase != GamePhase.GameOver)
        {
            rounds++;
            room.PickNumber(room.Round!.GuesserId, 1);
            var guesser = room.Round.GuesserId;
            var others = new[] { Alice, Bob, Carol }.Where(id => id != guesser).ToArray();
            foreach (var id in others)
            {
                room.SubmitClue(id, $"clue{rounds}{id.ToString()[^1]}");
            }

            room.RevealClues(others[0]);
            room.SubmitGuess(guesser, "always-wrong");
            room.JudgeGuess(others[0], accept: false);
            room.NextRound(Alice);
        }

        // Every wrong answer consumes two cards: 13 cards last only 7 rounds.
        await Assert.That(rounds).IsEqualTo(7);
        await Assert.That(room.History[0].Score).IsEqualTo(0);
    }

    [Test]
    public async Task Play_again_returns_to_lobby_and_keeps_history()
    {
        var room = Started3();
        RosterTests.PlayThroughGame(room);
        room.PlayAgain(Alice);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Lobby);
        await Assert.That(room.Score).IsEqualTo(0);
        await Assert.That(room.History.Count).IsEqualTo(1);
        await Assert.That(room.Players.Count).IsEqualTo(3);

        room.StartGame(Alice);
        RosterTests.PlayThroughGame(room);
        await Assert.That(room.History.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Score_counts_correct_rounds()
    {
        var room = Started3();
        var correct = 0;
        while (room.Phase != GamePhase.GameOver)
        {
            room.PickNumber(room.Round!.GuesserId, 1);
            var guesser = room.Round.GuesserId;
            var others = new[] { Alice, Bob, Carol }.Where(id => id != guesser).ToArray();
            foreach (var id in others)
            {
                room.SubmitClue(id, $"clue{room.Round.RoundNumber}{id.ToString()[^1]}");
            }

            room.RevealClues(others[0]);
            room.SubmitGuess(guesser, room.Round.MysteryWord!);
            correct++;
            room.NextRound(Alice);
        }

        await Assert.That(correct).IsEqualTo(GameRoom.CardsPerGame);
        await Assert.That(room.History[0].Score).IsEqualTo(GameRoom.CardsPerGame);
        await Assert.That(room.History[0].Rating).IsEqualTo("Perfect score!");
    }
}
