using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

public class RosterTests
{
    private static Flip7Room Empty() => Stacked(Num(1), Num(2), Num(3));

    [Test]
    public async Task First_joiner_becomes_host()
    {
        var room = Empty();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");

        await Assert.That(room.Players[0].IsHost).IsTrue();
        await Assert.That(room.Players[1].IsHost).IsFalse();
    }

    [Test]
    public async Task Room_caps_at_twenty_players()
    {
        var room = Empty();
        for (var i = 1; i <= Flip7Room.MaxPlayers; i++)
        {
            room.Join(Guid.NewGuid(), $"Player{i}");
        }

        var error = ExpectRuleError(() => room.Join(Guid.NewGuid(), "Unlucky"));
        await Assert.That(error.Message).Contains("20 players max");
    }

    [Test]
    public async Task Needs_three_to_start()
    {
        var room = Empty();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");

        var error = ExpectRuleError(() => room.StartGame(Alice));
        await Assert.That(error.Message).Contains("at least 3");
    }

    [Test]
    public async Task Rejoining_keeps_your_seat()
    {
        var room = Lobby3(Empty());
        room.Join(Bob, "Bob");

        await Assert.That(room.Players.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Joining_mid_game_spectates_until_the_next_one()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.Join(Dave, "Dave");

        await Assert.That(room.Players.Single(p => p.Id == Dave).IsSpectator).IsTrue();
        await Assert.That(room.Round!.Order).DoesNotContain(Dave);
    }

    [Test]
    public async Task Leaving_the_lobby_gives_up_the_seat()
    {
        var room = Lobby3(Empty());
        room.Leave(Bob);

        await Assert.That(room.Players.Count).IsEqualTo(2);
    }

    [Test]
    public async Task The_host_role_moves_on_when_the_host_leaves()
    {
        var room = Lobby3(Empty());
        room.Leave(Alice);

        await Assert.That(room.Host!.Id).IsEqualTo(Bob);
    }

    [Test]
    public async Task Leaving_mid_round_banks_what_you_had_and_the_round_carries_on()
    {
        var room = Started3(Num(4), Num(2), Num(3));
        room.Leave(Bob);

        await Assert.That(room.StatusOf(Bob)).IsEqualTo(RoundStatus.Stayed);
        await Assert.That(room.Round!.CurrentPlayerId).IsEqualTo(Carol);

        room.Stay(Carol);
        room.Stay(Alice);
        await Assert.That(room.Totals[Bob]).IsEqualTo(4);
    }

    [Test]
    public async Task The_host_can_sit_out_an_away_player()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerDisconnected(Carol);   // auto-stays her for this round
        room.BenchPlayer(Alice, Carol);

        await Assert.That(room.Players.Single(p => p.Id == Carol).BenchedForInactivity).IsTrue();
    }

    [Test]
    public async Task You_cannot_sit_out_someone_who_is_here()
    {
        var room = Started3(Num(1), Num(2), Num(3));

        var error = ExpectRuleError(() => room.BenchPlayer(Alice, Carol));
        await Assert.That(error.Message).Contains("only sit out a player who's away");
    }

    [Test]
    public async Task A_benched_player_is_left_out_of_the_next_round()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        room.Stay(Bob);
        room.Stay(Alice);
        room.NextRound(Alice);

        await Assert.That(room.Round!.Order).DoesNotContain(Carol);
    }

    [Test]
    public async Task The_bench_lifts_as_soon_as_they_are_back()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        room.PlayerConnected(Carol);

        await Assert.That(room.Players.Single(p => p.Id == Carol).IsSpectator).IsFalse();
    }

    [Test]
    public async Task The_bench_re_arms_if_they_drop_again()
    {
        // A flaky circuit must not quietly put them back in the game.
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        room.PlayerConnected(Carol);
        room.PlayerDisconnected(Carol);

        await Assert.That(room.Players.Single(p => p.Id == Carol).IsSpectator).IsTrue();
    }

    [Test]
    public async Task A_benched_player_who_came_back_is_dealt_in_next_game()
    {
        var room = Started3(Num(1), Num(2), Num(3));
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);
        room.PlayerConnected(Carol);
        room.Stay(Bob);
        room.Stay(Alice);
        room.NextRound(Alice);

        await Assert.That(room.Round!.Order).Contains(Carol);
    }

    [Test]
    public async Task Names_are_trimmed_and_capped()
    {
        var room = Empty();
        var player = room.Join(Alice, new string('x', 40));

        await Assert.That(player.Name.Length).IsEqualTo(Flip7Room.MaxNameLength);
    }

    [Test]
    public async Task A_nameless_join_is_refused()
    {
        var room = Empty();
        var error = ExpectRuleError(() => room.Join(Alice, "   "));
        await Assert.That(error.Message).Contains("Enter a name");
    }
}
