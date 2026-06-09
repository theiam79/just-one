using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

public class IllegalTransitionTests
{
    [Test]
    public async Task Actions_outside_their_phase_throw()
    {
        var room = Lobby3();
        ExpectRuleError(() => room.PickNumber(Alice, 1));
        ExpectRuleError(() => room.SubmitClue(Bob, "hint"));
        ExpectRuleError(() => room.RevealClues(Bob));
        ExpectRuleError(() => room.SubmitGuess(Alice, "word"));
        ExpectRuleError(() => room.Pass(Alice));
        ExpectRuleError(() => room.JudgeGuess(Bob, true));
        ExpectRuleError(() => room.NextRound(Alice));
        ExpectRuleError(() => room.PlayAgain(Alice));
        ExpectRuleError(() => room.SkipRound(Alice));
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Lobby);
    }

    [Test]
    public async Task Strangers_cannot_act_in_the_room()
    {
        var room = Started3();
        var stranger = Guid.NewGuid();
        var ex = ExpectRuleError(() => room.PickNumber(stranger, 1));
        await Assert.That(ex.Message).Contains("not in this room");
    }

    [Test]
    public async Task Starting_twice_throws()
    {
        var room = Started3();
        ExpectRuleError(() => room.StartGame(Alice));
        await Assert.That(room.Phase).IsEqualTo(GamePhase.NumberPick);
    }
}
