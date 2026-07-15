using JustOne.Core;
using static JustOne.Core.Tests.TestGame;

namespace JustOne.Core.Tests;

/// <summary>
/// The small-group variant: each clue-giver writes two different clues when the table is
/// short-handed, so one duplicate can't wipe out most of the clues (see issue #14).
/// </summary>
public class TwoCluesTests
{
    /// <summary>Three players with the variant left on Auto — so two clues each are owed.</summary>
    private static GameRoom InTwoClueWriting(TwoCluesMode mode = TwoCluesMode.Auto)
    {
        var room = Lobby3(); // Lobby3 pins Never; the variant fixtures opt back in
        room.SetTwoCluesMode(Alice, mode);
        room.StartGame(Alice);
        room.PickNumber(Alice, 1);
        return room;
    }

    /// <summary>A lobby of <paramref name="count"/> connected players; the first is host.</summary>
    private static (GameRoom Room, Guid[] Ids) LobbyOf(int count)
    {
        var room = NewRoom();
        var ids = new Guid[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = Guid.Parse($"00000000-0000-0000-0000-{i + 1:D12}");
            room.Join(ids[i], $"P{i + 1}");
            room.PlayerConnected(ids[i]);
        }

        return (room, ids);
    }

    [Test]
    public async Task Auto_asks_for_two_clues_when_short_handed()
    {
        var room = InTwoClueWriting();
        await Assert.That(room.Round!.CluesPerWriter).IsEqualTo(2);
    }

    [Test]
    [Arguments(3, 2)] // short-handed
    [Arguments(4, 2)] // the threshold itself — the boundary most easily got wrong
    [Arguments(5, 1)] // a full enough table
    public async Task Auto_follows_the_size_of_the_table(int players, int expectedClues)
    {
        var (room, ids) = LobbyOf(players);
        room.SetTwoCluesMode(ids[0], TwoCluesMode.Auto);
        room.StartGame(ids[0]);
        room.PickNumber(room.Round!.GuesserId, 1);

        await Assert.That(room.Round.CluesPerWriter).IsEqualTo(expectedClues);
    }

    [Test]
    public async Task Auto_follows_the_table_when_it_shrinks_mid_game()
    {
        // Auto re-reads the table each round, so sitting someone out can turn the variant on
        // between rounds. That's deliberate — being short-handed is exactly when it's needed.
        var (room, ids) = LobbyOf(5);
        room.SetTwoCluesMode(ids[0], TwoCluesMode.Auto);
        room.StartGame(ids[0]);
        room.PickNumber(room.Round!.GuesserId, 1);
        await Assert.That(room.Round.CluesPerWriter).IsEqualTo(1);

        room.SkipRound(ids[0]);
        room.PlayerDisconnected(ids[4]);
        room.BenchPlayer(ids[0], ids[4]); // down to four at the table
        room.NextRound(ids[0]);
        room.PickNumber(room.Round!.GuesserId, 1);

        await Assert.That(room.Round.CluesPerWriter).IsEqualTo(2);
    }

    [Test]
    public async Task Host_can_force_the_variant_off_when_short_handed()
    {
        var room = InTwoClueWriting(TwoCluesMode.Never);
        await Assert.That(room.Round!.CluesPerWriter).IsEqualTo(1);
        room.SubmitClue(Bob, "alpha"); // a single clue is enough
        await Assert.That(room.Round.Clues.Only(Bob).Text).IsEqualTo("alpha");
    }

    [Test]
    public async Task Host_can_force_the_variant_on_with_a_full_table()
    {
        var room = InTwoClueWriting(TwoCluesMode.Always);
        await Assert.That(room.Round!.CluesPerWriter).IsEqualTo(2);
    }

    [Test]
    public async Task Only_the_host_sets_the_mode_and_only_in_the_lobby()
    {
        var room = Lobby3();
        var ex = ExpectRuleError(() => room.SetTwoCluesMode(Bob, TwoCluesMode.Always));
        await Assert.That(ex.Message).Contains("host");

        var mid = InTwoClueWriting();
        ExpectRuleError(() => mid.SetTwoCluesMode(Alice, TwoCluesMode.Never));
        await Assert.That(mid.Round!.CluesPerWriter).IsEqualTo(2); // unchanged mid-round
    }

    [Test]
    public async Task Both_clues_are_submitted_together()
    {
        var room = InTwoClueWriting();
        room.SubmitClues(Bob, "teeth", "bristles");

        var clues = room.Round!.Clues[Bob];
        await Assert.That(clues.Count).IsEqualTo(2);
        await Assert.That(clues.Select(c => c.Text)).IsEquivalentTo(new[] { "teeth", "bristles" });
    }

    [Test]
    public async Task A_writer_is_not_done_until_both_clues_are_in()
    {
        var room = InTwoClueWriting();
        var ex = ExpectRuleError(() => room.SubmitClue(Bob, "teeth")); // only one
        await Assert.That(ex.Message).Contains("all 2");
        await Assert.That(room.Round!.Clues.ContainsKey(Bob)).IsFalse();

        room.SubmitClues(Bob, "teeth", "bristles");
        room.SubmitClues(Carol, "paste", "brush");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueReview); // now everyone's done
    }

    [Test]
    public async Task A_blank_second_clue_is_rejected()
    {
        var room = InTwoClueWriting();
        var ex = ExpectRuleError(() => room.SubmitClues(Bob, "teeth", "   "));
        await Assert.That(ex.Message).Contains("all 2");
    }

    [Test]
    public async Task Submitting_more_clues_than_owed_is_rejected()
    {
        var room = InTwoClueWriting();
        var tooMany = ExpectRuleError(() => room.SubmitClues(Bob, "a", "b", "c"));
        await Assert.That(tooMany.Message).Contains("Only 2");

        var single = InTwoClueWriting(TwoCluesMode.Never);
        var ex = ExpectRuleError(() => single.SubmitClues(Bob, "a", "b"));
        await Assert.That(ex.Message).Contains("Just one");
    }

    [Test]
    public async Task A_rejected_clue_says_which_one_it_was()
    {
        var room = InTwoClueWriting();
        var tooLong = ExpectRuleError(() => room.SubmitClues(Bob, "teeth", new string('a', 31)));
        await Assert.That(tooLong.Message).StartsWith("Clue 2:");

        var mystery = room.Round!.MysteryWord!;
        var isMystery = ExpectRuleError(() => room.SubmitClues(Bob, mystery, "bristles"));
        await Assert.That(isMystery.Message).StartsWith("Clue 1");
        await Assert.That(isMystery.Message).Contains("mystery word");
    }

    [Test]
    public async Task Your_own_two_clues_have_to_differ()
    {
        var room = InTwoClueWriting();
        var ex = ExpectRuleError(() => room.SubmitClues(Bob, "Teeth", "teeth")); // same once normalized
        await Assert.That(ex.Message).Contains("different from each other");
        await Assert.That(room.Round!.Clues.ContainsKey(Bob)).IsFalse();
    }

    [Test]
    public async Task Neither_clue_can_be_the_mystery_word()
    {
        var room = InTwoClueWriting();
        var mystery = room.Round!.MysteryWord!;
        var ex = ExpectRuleError(() => room.SubmitClues(Bob, "teeth", mystery.ToUpperInvariant()));
        await Assert.That(ex.Message).Contains("mystery word");
    }

    [Test]
    public async Task Duplicates_cancel_across_writers_not_just_within_them()
    {
        var room = InTwoClueWriting();
        room.SubmitClues(Bob, "teeth", "bristles");
        room.SubmitClues(Carol, "TEETH", "paste"); // "teeth" collides with Bob's

        var all = room.Round!.Clues.All().ToList();
        await Assert.That(all.Count).IsEqualTo(4);
        await Assert.That(all.Where(c => c.Normalized == "teeth").All(c => c.AutoCancelled)).IsTrue();
        // The writers' other clues are untouched, which is the point of the variant:
        // a duplicate no longer leaves the guesser with nothing.
        await Assert.That(all.Count(c => c.Visible)).IsEqualTo(2);
    }

    [Test]
    public async Task Resubmitting_replaces_both_clues()
    {
        var room = InTwoClueWriting();
        room.SubmitClues(Bob, "teeth", "bristles");
        room.SubmitClues(Bob, "paste", "brush");

        await Assert.That(room.Round!.Clues[Bob].Select(c => c.Text)).IsEquivalentTo(new[] { "paste", "brush" });
        await Assert.That(room.Round.Clues[Bob].Count).IsEqualTo(2);
    }

    [Test]
    public async Task Taking_it_back_retracts_both_clues()
    {
        var room = InTwoClueWriting();
        room.SubmitClues(Bob, "teeth", "bristles");
        room.UnsubmitClue(Bob);

        await Assert.That(room.Round!.Clues.ContainsKey(Bob)).IsFalse();
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting);

        room.SubmitClues(Carol, "paste", "brush");
        await Assert.That(room.Phase).IsEqualTo(GamePhase.ClueWriting); // Bob still owes his
    }

    [Test]
    public async Task Reviewers_cancel_one_of_a_writers_two_clues()
    {
        var room = InTwoClueWriting();
        room.SubmitClues(Bob, "teeth", "bristles");
        room.SubmitClues(Carol, "paste", "brush");

        room.ToggleClueCancellation(Carol, Bob, 1); // just the second one

        await Assert.That(room.Round!.Clues[Bob][0].Visible).IsTrue();
        await Assert.That(room.Round.Clues[Bob][1].Visible).IsFalse();
    }

    [Test]
    public async Task Cancelling_a_clue_index_that_does_not_exist_is_rejected()
    {
        var room = InTwoClueWriting();
        room.SubmitClues(Bob, "teeth", "bristles");
        room.SubmitClues(Carol, "paste", "brush");

        ExpectRuleError(() => room.ToggleClueCancellation(Carol, Bob, 2));
        ExpectRuleError(() => room.ToggleClueCancellation(Carol, Bob, -1));
    }

    [Test]
    public async Task The_count_is_fixed_for_the_round_even_if_the_table_grows()
    {
        var room = InTwoClueWriting();
        await Assert.That(room.Round!.CluesPerWriter).IsEqualTo(2);

        // Someone turning up mid-round spectates and must not move the goalposts.
        room.Join(Dave, "Dave");
        room.PlayerConnected(Dave);
        await Assert.That(room.Round.CluesPerWriter).IsEqualTo(2);

        room.SubmitClues(Bob, "teeth", "bristles");
        await Assert.That(room.Round.Clues[Bob].Count).IsEqualTo(2);
    }
}
