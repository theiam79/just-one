using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

/// <summary>The room keeps each round's points, so a scorecard can show the game round by round.</summary>
public class ScorecardTests
{
    [Test]
    public async Task A_finished_round_is_recorded_with_each_players_points()
    {
        var room = Started3(Num(5), Num(4), Num(3));   // deal: Bob 5, Carol 4, Alice 3
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        await Assert.That(room.RoundScores.Count).IsEqualTo(1);
        await Assert.That(room.RoundScores[0][Bob]).IsEqualTo(5);
        await Assert.That(room.RoundScores[0][Carol]).IsEqualTo(4);
        await Assert.That(room.RoundScores[0][Alice]).IsEqualTo(3);
    }

    [Test]
    public async Task A_busted_line_records_zero()
    {
        var room = Started3(Num(5), Num(4), Num(3), Num(5));   // Bob draws a second 5
        room.Hit(Bob);      // bust
        room.Stay(Carol);
        room.Stay(Alice);

        await Assert.That(room.RoundScores[0][Bob]).IsEqualTo(0);
        await Assert.That(room.Totals[Bob]).IsEqualTo(0);
    }

    [Test]
    public async Task Rounds_accumulate_and_totals_are_their_sum()
    {
        var room = Started3(
            Num(5), Num(4), Num(3),   // round 1 deal
            Num(6), Num(7), Num(8));  // round 2 deal
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);            // round 1 ends

        room.NextRound(Alice);
        while (room.Round!.CurrentPlayerId is { } current)
        {
            room.Stay(current);      // round 2: everyone banks their dealt card
        }

        await Assert.That(room.RoundScores.Count).IsEqualTo(2);
        foreach (var id in new[] { Alice, Bob, Carol })
        {
            await Assert.That(room.Totals[id]).IsEqualTo(room.RoundScores[0][id] + room.RoundScores[1][id]);
        }
    }
}
