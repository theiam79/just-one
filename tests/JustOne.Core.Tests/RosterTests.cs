using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

public class RosterTests
{
    [Test]
    public async Task First_joiner_becomes_host()
    {
        var room = NewRoom();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        await Assert.That(room.Players[0].IsHost).IsTrue();
        await Assert.That(room.Players[1].IsHost).IsFalse();
    }

    [Test]
    public async Task Room_caps_at_twelve_players()
    {
        var room = NewRoom();
        for (var i = 1; i <= GameRoom.MaxPlayers; i++)
        {
            room.Join(Guid.NewGuid(), $"Player{i}");
        }

        var ex = ExpectRuleError(() => room.Join(Guid.NewGuid(), "Unlucky"));
        await Assert.That(ex.Message).Contains("full");
    }

    [Test]
    public async Task Rejoining_with_same_id_reclaims_seat()
    {
        var room = Lobby3();
        var before = room.Players.Count;
        var player = room.Join(Alice, "Alice2");
        await Assert.That(room.Players.Count).IsEqualTo(before);
        await Assert.That(player.Name).IsEqualTo("Alice2");
        await Assert.That(player.IsHost).IsTrue();
    }

    [Test]
    public async Task Joining_mid_game_makes_you_a_spectator()
    {
        var room = Started3();
        var dave = room.Join(Dave, "Dave");
        await Assert.That(dave.IsSpectator).IsTrue();
    }

    [Test]
    public async Task Spectators_become_players_when_next_game_starts()
    {
        var room = Started3();
        room.Join(Dave, "Dave");
        room.PlayerConnected(Dave);
        PlayThroughGame(room);
        room.PlayAgain(Alice);
        await Assert.That(room.Players.First(p => p.Id == Dave).IsSpectator).IsFalse();
        room.StartGame(Alice);
        await Assert.That(room.Players.All(p => !p.IsSpectator)).IsTrue();
    }

    [Test]
    public async Task Host_migrates_when_host_leaves()
    {
        var room = Lobby3();
        room.Leave(Alice);
        await Assert.That(room.Players.Count).IsEqualTo(2);
        await Assert.That(room.Host!.Id).IsEqualTo(Bob);
    }

    [Test]
    public async Task Leaving_mid_game_keeps_seat_as_spectator()
    {
        var room = InClueWriting();
        room.Leave(Carol);
        var carol = room.Players.First(p => p.Id == Carol);
        await Assert.That(carol.IsSpectator).IsTrue();
    }

    [Test]
    public async Task Leaving_mid_clue_writing_skips_your_clue()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        room.Leave(Carol);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
    }

    [Test]
    public async Task Connection_counter_handles_multiple_tabs()
    {
        var room = NewRoom();
        var alice = room.Join(Alice, "Alice");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Alice);
        room.PlayerDisconnected(Alice);
        await Assert.That(alice.IsConnected).IsTrue();
        room.PlayerDisconnected(Alice);
        await Assert.That(alice.IsConnected).IsFalse();
        room.PlayerDisconnected(Alice);
        await Assert.That(alice.ConnectionCount).IsEqualTo(0);
    }

    /// <summary>Plays every remaining card as a pass to reach GameOver.</summary>
    internal static void PlayThroughGame(GameRoom room)
    {
        while (room.Phase != GamePhase.GameOver)
        {
            switch (room.Phase)
            {
                case GamePhase.NumberPick:
                    room.SkipRound(Alice);
                    break;
                case GamePhase.RoundResult:
                    room.NextRound(Alice);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected phase {room.Phase}");
            }
        }
    }
}
