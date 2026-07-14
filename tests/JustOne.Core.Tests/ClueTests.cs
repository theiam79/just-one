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
    public async Task Unsubmitting_removes_the_clue_and_keeps_the_phase_open()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        room.UnsubmitClue(Bob);
        await Assert.That(room.Round!.Clues.ContainsKey(Bob)).IsFalse();
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    public async Task Unsubmitting_prevents_being_cut_off_when_others_finish()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        room.UnsubmitClue(Bob);          // Bob is reworking his clue
        room.SubmitClue(Carol, "beta");  // everyone else finishes

        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting); // Bob is not cut off

        room.SubmitClue(Bob, "gamma");   // Bob finishes his rework
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
        await Assert.That(room.Round!.Clues[Bob].Text).IsEqualTo("gamma");
    }

    [Test]
    public async Task Cannot_unsubmit_a_clue_you_never_submitted()
    {
        var room = InClueWriting();
        var ex = ExpectRuleError(() => room.UnsubmitClue(Bob));
        await Assert.That(ex.Message).Contains("take back");
    }

    [Test]
    public async Task Guesser_cannot_unsubmit()
    {
        var room = InClueWriting();
        var ex = ExpectRuleError(() => room.UnsubmitClue(Alice));
        await Assert.That(ex.Message).Contains("not writing");
    }

    [Test]
    public async Task Cannot_unsubmit_after_clue_writing_is_over()
    {
        var room = InClueReview(); // both clues in, phase advanced to ClueReview
        ExpectRuleError(() => room.UnsubmitClue(Bob));
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
    }

    [Test]
    public async Task Host_can_skip_a_writer_who_unsubmitted()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        room.UnsubmitClue(Bob);
        room.SubmitClue(Carol, "beta");
        room.SkipPlayerClue(Alice, Bob); // host unblocks the reworking player
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
    }

    [Test]
    public async Task Leaving_after_unsubmit_still_finishes_the_round()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "alpha");
        room.UnsubmitClue(Bob);
        room.SubmitClue(Carol, "beta");
        room.Leave(Bob); // Bob vanishes mid-rework; the round must not get stuck
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview);
    }

    [Test]
    public async Task Multi_word_clue_is_accepted_and_stored_cleaned()
    {
        var room = InClueWriting();
        room.SubmitClue(Bob, "  New   York ");
        await Assert.That(room.Round!.Clues[Bob].Text).IsEqualTo("New York");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);
    }

    [Test]
    public async Task Multi_word_duplicates_auto_cancel_across_whitespace_and_case()
    {
        var room = InClueReview(bobClue: "New York", carolClue: "new  york");
        await Assert.That(room.Round!.Clues[Bob].AutoCancelled).IsTrue();
        await Assert.That(room.Round.Clues[Carol].AutoCancelled).IsTrue();
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
