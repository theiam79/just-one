using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

public class GuessTests
{
    [Test]
    public async Task Exact_match_scores_immediately_ignoring_case()
    {
        var room = InGuessing();
        room.SubmitGuess(Alice, room.Round!.MysteryWord!.ToUpperInvariant());
        await Assert.That(room.Phase).IsEqualTo(GamePhase.RoundResult);
        await Assert.That(room.Round.Outcome).IsEqualTo(RoundOutcome.Correct);
        await Assert.That(room.Score).IsEqualTo(1);
        await Assert.That(room.DeckCount).IsEqualTo(12);
    }

    [Test]
    public async Task Pass_discards_the_card_without_penalty()
    {
        var room = InGuessing();
        room.Pass(Alice);
        await Assert.That(room.Round!.Outcome).IsEqualTo(RoundOutcome.Passed);
        await Assert.That(room.Score).IsEqualTo(0);
        await Assert.That(room.DeckCount).IsEqualTo(12);
    }

    [Test]
    public async Task Near_miss_goes_to_judging()
    {
        var room = InGuessing();
        room.SubmitGuess(Alice, "definitely-not-the-word");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Judging);
        await Assert.That(room.Round!.Outcome).IsNull();
    }

    [Test]
    public async Task Accepted_judgment_scores_the_point()
    {
        var room = InGuessing();
        room.SubmitGuess(Alice, "close-enough");
        room.JudgeGuess(Bob, accept: true);
        await Assert.That(room.Round!.Outcome).IsEqualTo(RoundOutcome.Correct);
        await Assert.That(room.Score).IsEqualTo(1);
        await Assert.That(room.DeckCount).IsEqualTo(12);
    }

    [Test]
    public async Task Rejected_judgment_loses_this_card_and_the_next()
    {
        var room = InGuessing();
        room.SubmitGuess(Alice, "way-off");
        room.JudgeGuess(Bob, accept: false);
        await Assert.That(room.Round!.Outcome).IsEqualTo(RoundOutcome.Wrong);
        await Assert.That(room.Score).IsEqualTo(0);
        await Assert.That(room.DeckCount).IsEqualTo(11);
    }

    [Test]
    public async Task Guesser_cannot_judge_their_own_guess()
    {
        var room = InGuessing();
        room.SubmitGuess(Alice, "way-off");
        ExpectRuleError(() => room.JudgeGuess(Alice, accept: true));
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Judging);
    }

    [Test]
    public async Task Only_guesser_can_guess_or_pass()
    {
        var room = InGuessing();
        ExpectRuleError(() => room.SubmitGuess(Bob, "sneaky"));
        ExpectRuleError(() => room.Pass(Carol));
        await Assert.That(room.Phase).IsEqualTo(GamePhase.Guessing);
    }

    [Test]
    public async Task Wrong_guess_on_the_last_card_is_safe()
    {
        var room = Started3();
        // Burn cards until only the in-play card remains.
        while (room.DeckCount > 0)
        {
            room.SkipRound(Alice);
            room.NextRound(Alice);
        }

        room.PickNumber(room.Round!.GuesserId, 1);
        var guesser = room.Round.GuesserId;
        foreach (var id in new[] { Alice, Bob, Carol }.Where(id => id != guesser))
        {
            room.SubmitClue(id, $"clue-{id.ToString()[^1]}");
        }

        room.RevealClues(new[] { Alice, Bob, Carol }.First(id => id != guesser));
        room.SubmitGuess(guesser, "wrong-answer");
        room.JudgeGuess(new[] { Alice, Bob, Carol }.First(id => id != guesser), accept: false);
        await Assert.That(room.Round.Outcome).IsEqualTo(RoundOutcome.Wrong);
        room.NextRound(Alice);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.GameOver);
    }
}
