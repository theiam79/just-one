namespace Party.Core.Tests;

public class RoomBaseTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static TestRoom Lobby3()
    {
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        return room;
    }

    private static GameRuleException ExpectRuleError(Action action)
    {
        try
        {
            action();
        }
        catch (GameRuleException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected a GameRuleException, but the action succeeded.");
    }

    // ---- Joining ----

    [Test]
    public async Task First_joiner_becomes_host()
    {
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");

        await Assert.That(room.Players[0].IsHost).IsTrue();
        await Assert.That(room.Players[1].IsHost).IsFalse();
    }

    [Test]
    public async Task Rejoining_with_the_same_id_keeps_the_seat()
    {
        var room = Lobby3();
        room.Join(Bob, "Bob");

        await Assert.That(room.Players.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Rejoining_can_change_the_name()
    {
        var room = Lobby3();
        room.Join(Bob, "Robert");

        await Assert.That(room.Players.Single(p => p.Id == Bob).Name).IsEqualTo("Robert");
    }

    [Test]
    public async Task Rejoining_without_a_name_keeps_the_old_one()
    {
        var room = Lobby3();
        room.Join(Bob, "  ");

        await Assert.That(room.Players.Single(p => p.Id == Bob).Name).IsEqualTo("Bob");
    }

    [Test]
    public async Task A_nameless_join_is_refused()
    {
        var room = new TestRoom();
        var error = ExpectRuleError(() => room.Join(Alice, "   "));
        await Assert.That(error.Message).Contains("Enter a name");
    }

    [Test]
    public async Task Names_are_trimmed_and_capped()
    {
        var room = new TestRoom();
        var player = room.Join(Alice, "  " + new string('x', 40) + "  ");

        await Assert.That(player.Name.Length).IsEqualTo(RoomBase.MaxNameLength);
    }

    [Test]
    public async Task The_room_fills_up()
    {
        var room = new TestRoom(maxSeats: 4);
        for (var i = 0; i < 4; i++)
        {
            room.Join(Guid.NewGuid(), $"Player{i}");
        }

        var error = ExpectRuleError(() => room.Join(Guid.NewGuid(), "Unlucky"));
        await Assert.That(error.Message).Contains("4 players max");
    }

    [Test]
    public async Task Joining_mid_game_watches_until_the_next_one()
    {
        var room = Lobby3();
        room.Start();
        room.Join(Guid.NewGuid(), "Dave");

        await Assert.That(room.Players.Last().IsSpectator).IsTrue();
    }

    // ---- Leaving ----

    [Test]
    public async Task Leaving_the_lobby_gives_up_the_seat()
    {
        var room = Lobby3();
        room.Leave(Bob);

        await Assert.That(room.Players.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Leaving_mid_game_keeps_the_seat_but_sidelines_them()
    {
        // The seat stays because scores still point at it.
        var room = Lobby3();
        room.Start();
        room.Leave(Bob);

        await Assert.That(room.Players.Count).IsEqualTo(3);
        await Assert.That(room.Players.Single(p => p.Id == Bob).IsSpectator).IsTrue();
        await Assert.That(room.Sidelined).IsEquivalentTo(new[] { Bob });
    }

    [Test]
    public async Task Leaving_once_the_game_is_over_gives_up_the_seat()
    {
        var room = Lobby3();
        room.Start();
        room.GameFinished = true;
        room.Leave(Bob);

        await Assert.That(room.Players.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_spectator_leaving_gives_up_their_seat_even_mid_game()
    {
        var room = Lobby3();
        room.Start();
        var dave = Guid.NewGuid();
        room.Join(dave, "Dave");   // spectator
        room.Leave(dave);

        await Assert.That(room.Players.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Leaving_a_room_you_are_not_in_does_nothing()
    {
        var room = Lobby3();
        room.Leave(Guid.NewGuid());

        await Assert.That(room.Players.Count).IsEqualTo(3);
    }

    [Test]
    public async Task The_host_role_moves_on_when_the_host_leaves()
    {
        var room = Lobby3();
        room.Leave(Alice);

        await Assert.That(room.Host!.Id).IsEqualTo(Bob);
    }

    [Test]
    public async Task The_host_role_prefers_someone_actually_playing()
    {
        var room = Lobby3();
        room.Start();
        var dave = Guid.NewGuid();
        room.Join(dave, "Dave");   // spectator
        room.GameFinished = true;
        room.Leave(Alice);

        await Assert.That(room.Host!.Id).IsEqualTo(Bob);
    }

    [Test]
    public async Task The_last_player_leaving_leaves_no_host()
    {
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.Leave(Alice);

        await Assert.That(room.Host).IsNull();
    }

    // ---- Connections ----

    [Test]
    public async Task Tabs_are_counted()
    {
        var room = Lobby3();
        room.PlayerConnected(Alice);   // a second tab

        await Assert.That(room.Players.Single(p => p.Id == Alice).ConnectionCount).IsEqualTo(2);
        room.PlayerDisconnected(Alice);
        await Assert.That(room.Players.Single(p => p.Id == Alice).IsConnected).IsTrue();
        room.PlayerDisconnected(Alice);
        await Assert.That(room.Players.Single(p => p.Id == Alice).IsConnected).IsFalse();
    }

    [Test]
    public async Task Disconnecting_below_zero_is_ignored()
    {
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.PlayerDisconnected(Alice);
        room.PlayerDisconnected(Alice);

        await Assert.That(room.Players.Single(p => p.Id == Alice).ConnectionCount).IsEqualTo(0);
    }

    [Test]
    public async Task Connection_changes_are_announced()
    {
        // A turn game needs this: it can't be left waiting on someone who has gone.
        var room = Lobby3();
        room.ConnectionChanges.Clear();
        room.PlayerDisconnected(Bob);

        await Assert.That(room.ConnectionChanges).IsEquivalentTo(new[] { Bob });
    }

    // ---- The sticky bench ----

    [Test]
    public async Task Only_an_away_player_can_be_sat_out()
    {
        var room = Lobby3();
        var error = ExpectRuleError(() => room.BenchPlayer(Alice, Bob));
        await Assert.That(error.Message).Contains("only sit out a player who's away");
    }

    [Test]
    public async Task Sitting_someone_out_sidelines_them()
    {
        var room = Lobby3();
        room.Start();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);

        await Assert.That(room.Players.Single(p => p.Id == Bob).BenchedForInactivity).IsTrue();
        await Assert.That(room.Sidelined).Contains(Bob);
    }

    [Test]
    public async Task Sitting_out_the_same_player_twice_is_refused()
    {
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);

        var error = ExpectRuleError(() => room.BenchPlayer(Alice, Bob));
        await Assert.That(error.Message).Contains("already sitting out");
    }

    [Test]
    public async Task Sitting_out_a_stranger_is_refused()
    {
        var room = Lobby3();
        var error = ExpectRuleError(() => room.BenchPlayer(Alice, Guid.NewGuid()));
        await Assert.That(error.Message).Contains("isn't in this room");
    }

    [Test]
    public async Task The_bench_lifts_the_moment_they_are_back()
    {
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);
        room.PlayerConnected(Bob);

        await Assert.That(room.Players.Single(p => p.Id == Bob).IsSpectator).IsFalse();
    }

    [Test]
    public async Task The_bench_re_arms_if_they_drop_again()
    {
        // The point of it being sticky: a flaky circuit must not quietly deal them back in.
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);
        room.PlayerConnected(Bob);
        room.PlayerDisconnected(Bob);

        await Assert.That(room.Players.Single(p => p.Id == Bob).IsSpectator).IsTrue();
        await Assert.That(room.Players.Single(p => p.Id == Bob).BenchedForInactivity).IsTrue();
    }

    [Test]
    public async Task A_benched_player_who_is_back_is_dealt_into_the_next_game()
    {
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);
        room.PlayerConnected(Bob);
        room.NewGame();

        await Assert.That(room.SeatedIds).Contains(Bob);
        await Assert.That(room.Players.Single(p => p.Id == Bob).BenchedForInactivity).IsFalse();
    }

    [Test]
    public async Task A_benched_player_still_away_sits_the_next_game_out_too()
    {
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);
        room.NewGame();

        await Assert.That(room.SeatedIds).DoesNotContain(Bob);
        await Assert.That(room.Players.Single(p => p.Id == Bob).BenchedForInactivity).IsTrue();
    }

    [Test]
    public async Task A_new_game_deals_the_watchers_in()
    {
        var room = Lobby3();
        room.Start();
        var dave = Guid.NewGuid();
        room.Join(dave, "Dave");
        room.PlayerConnected(dave);
        room.NewGame();

        await Assert.That(room.SeatedIds).Contains(dave);
    }

    // ---- Starting ----

    [Test]
    public async Task A_game_needs_enough_players()
    {
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");

        var error = ExpectRuleError(room.Start);
        await Assert.That(error.Message).Contains("at least 3");
    }

    [Test]
    public async Task A_benched_away_player_does_not_count_towards_the_minimum()
    {
        // Otherwise a "full" room could start a game nobody can actually play.
        var room = Lobby3();
        room.PlayerDisconnected(Bob);
        room.BenchPlayer(Alice, Bob);

        var error = ExpectRuleError(room.Start);
        await Assert.That(error.Message).Contains("at least 3");
    }

    // ---- Host powers ----

    [Test]
    public async Task Host_only_means_host_only()
    {
        var room = Lobby3();
        var error = ExpectRuleError(() => room.AsHost(Bob));
        await Assert.That(error.Message).Contains("Only the host (Alice)");
    }

    [Test]
    public async Task Anyone_may_drive_when_the_host_is_away()
    {
        // So a room whose host closed their laptop doesn't become unplayable.
        var room = Lobby3();
        room.PlayerDisconnected(Alice);

        room.AsHost(Bob);
    }

    [Test]
    public async Task A_stranger_has_no_powers_at_all()
    {
        var room = Lobby3();
        var error = ExpectRuleError(() => room.AsHost(Guid.NewGuid()));
        await Assert.That(error.Message).Contains("not in this room");
    }

    [Test]
    public async Task Spectators_cannot_act()
    {
        var room = Lobby3();
        room.Start();
        var dave = Guid.NewGuid();
        room.Join(dave, "Dave");

        var error = ExpectRuleError(() => room.AsSeated(dave));
        await Assert.That(error.Message).Contains("Spectators can't do that");
    }

    [Test]
    public async Task The_room_knows_its_code()
    {
        await Assert.That(new TestRoom().Code).IsEqualTo("TEST");
    }
}
