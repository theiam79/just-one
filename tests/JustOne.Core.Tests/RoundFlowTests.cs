using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

public class RoundFlowTests
{
    [Test]
    public async Task Only_guesser_picks_the_number()
    {
        var room = Started3();
        ExpectRuleError(() => room.PickNumber(Bob, 3));
        room.PickNumber(Alice, 3);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    [Arguments(0)]
    [Arguments(6)]
    public async Task Number_must_be_one_to_five(int number)
    {
        var room = Started3();
        var ex = ExpectRuleError(() => room.PickNumber(Alice, number));
        await Assert.That(ex.Message).Contains("1 to 5");
    }

    [Test]
    public async Task Mystery_word_is_the_chosen_card_word()
    {
        var room = Started3();
        var expected = room.Round!.Card.Words[3];
        room.PickNumber(Alice, 4);
        await Assert.That(room.Round.MysteryWord).IsEqualTo(expected);
    }

    [Test]
    public async Task Guesser_rotates_by_seat_order()
    {
        var room = InGuessing();
        room.Pass(Alice);
        room.NextRound(Alice);
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Bob);
        await Assert.That(room.Round.RoundNumber).IsEqualTo(2);
    }

    [Test]
    public async Task Rotation_skips_disconnected_players()
    {
        var room = InGuessing();
        room.PlayerDisconnected(Bob);
        room.Pass(Alice);
        room.NextRound(Alice);
        await Assert.That(room.Round!.GuesserId).IsEqualTo(Carol);
    }

    [Test]
    public async Task Host_can_skip_a_stuck_round_as_a_pass()
    {
        var room = InClueWriting();
        room.SkipRound(Alice);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.RoundResult);
        await Assert.That(room.Round!.Outcome).IsEqualTo(RoundOutcome.Passed);
        await Assert.That(room.Score).IsEqualTo(0);
    }
}
