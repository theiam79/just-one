using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

public class GameStartTests
{
    [Test]
    public async Task Non_host_cannot_start_while_host_is_connected()
    {
        var room = Lobby3();
        var ex = ExpectRuleError(() => room.StartGame(Bob));
        await Assert.That(ex.Message).Contains("host");
    }

    [Test]
    public async Task Anyone_can_start_if_host_is_disconnected()
    {
        var room = Lobby3();
        room.PlayerDisconnected(Alice);
        room.StartGame(Bob);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.NumberPick);
    }

    [Test]
    public async Task Needs_at_least_three_players()
    {
        var room = NewRoom();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.PlayerConnected(Alice);
        var ex = ExpectRuleError(() => room.StartGame(Alice));
        await Assert.That(ex.Message).Contains("3 players");
    }

    [Test]
    public async Task Deck_is_thirteen_cards_of_five_distinct_words()
    {
        var room = Started3();
        await Assert.That(room.DeckCount).IsEqualTo(GameRoom.CardsPerGame - 1); // one card in play
        await Assert.That(room.CardsLeft).IsEqualTo(GameRoom.CardsPerGame);
        await Assert.That(room.Round!.Card.Words.Count).IsEqualTo(GameRoom.WordsPerCard);
    }

    [Test]
    public async Task Deck_is_deterministic_with_seeded_rng()
    {
        var room1 = Started3(seed: 7);
        var room2 = Started3(seed: 7);
        await Assert.That(room1.Round!.Card.Words).IsEquivalentTo(room2.Round!.Card.Words);
    }

    [Test]
    public async Task First_guesser_is_first_seat()
    {
        var room = Started3();
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Alice);
        await Assert.That(room.Round.RoundNumber).IsEqualTo(1);
    }
}
