using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

public class ClueTests
{
    [Test]
    public async Task Guesser_cannot_submit_a_clue()
    {
        var room = InClueWriting();
        var ex = ExpectRuleError(() => room.SubmitClue(Alice, "hint"));
        await Assert.That(ex.Message).Contains("not writing");
    }

    [Test]
    public async Task Spectator_cannot_submit_a_clue()
    {
        var room = InClueWriting();
        room.Join(Dave, "Dave");
        ExpectRuleError(() => room.SubmitClue(Dave, "hint"));
        await Assert.That(room.Round!.Clues).IsEmpty();
    }

    [Test]
    public async Task Resubmitting_overwrites_until_everyone_is_done()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "first");
        room.SubmitClue(Bob, "second");
        await Assert.That(room.Round!.Clues[Bob].Text).IsEqualTo("second");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    public async Task Clue_matching_the_mystery_word_is_rejected()
    {
        var room = InClueWriting();
        var mystery = room.Round!.MysteryWord!;
        var ex = ExpectRuleError(() => room.SubmitClue(Bob, mystery.ToUpperInvariant()));
        await Assert.That(ex.Message).Contains("mystery word");
    }

    [Test]
    public async Task Phase_advances_only_when_all_expected_clues_are_in()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
        room.SubmitClue(Carol, "beta");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
    }

    [Test]
    public async Task Host_skipping_a_pending_writer_unblocks_the_phase()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        room.SkipPlayerClue(Alice, Carol);
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
        await Assert.That(room.Round!.Clues.ContainsKey(Carol)).IsFalse();
    }

    [Test]
    public async Task Identical_clues_are_auto_cancelled_case_and_accent_insensitively()
    {
        var room = InClueReview(bobClue: "Café", carolClue: "cafe");
        await Assert.That(room.Round!.Clues[Bob].AutoCancelled).IsTrue();
        await Assert.That(room.Round.Clues[Carol].AutoCancelled).IsTrue();
        await Assert.That(room.Round.Clues.Values.Count(c => c.Visible)).IsEqualTo(0);
    }

    [Test]
    public async Task Distinct_clues_survive_auto_cancellation()
    {
        var room = InClueReview(bobClue: "alpha", carolClue: "beta");
        await Assert.That(room.Round!.Clues.Values.Count(c => c.Visible)).IsEqualTo(2);
    }

    [Test]
    public async Task Reviewers_can_toggle_manual_cancellation()
    {
        var room = InClueReview();
        room.ToggleClueCancellation(Carol, Bob);
        await Assert.That(room.Round!.Clues[Bob].Visible).IsFalse();
        room.ToggleClueCancellation(Carol, Bob);
        await Assert.That(room.Round.Clues[Bob].Visible).IsTrue();
    }

    [Test]
    public async Task Auto_cancelled_clues_cannot_be_reinstated()
    {
        var room = InClueReview(bobClue: "same", carolClue: "same");
        var ex = ExpectRuleError(() => room.ToggleClueCancellation(Carol, Bob));
        await Assert.That(ex.Message).Contains("stay cancelled");
    }

    [Test]
    public async Task Guesser_cannot_review_clues()
    {
        var room = InClueReview();
        ExpectRuleError(() => room.ToggleClueCancellation(Alice, Bob));
        ExpectRuleError(() => room.RevealClues(Alice));
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
    }
}
