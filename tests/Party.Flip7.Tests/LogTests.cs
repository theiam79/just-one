using Party.Core;
using static Party.Flip7.Tests.TestGame;

namespace Party.Flip7.Tests;

/// <summary>
/// The round log is the game's play-by-play. What matters is that each notable thing that
/// happens leaves a line, phrased for a human and tagged so the UI can style it — including the
/// provenance the table itself throws away, like who played a Freeze on whom.
/// </summary>
public class LogTests
{
    private static IEnumerable<string> Texts(Flip7Room room) =>
        room.Feed.OfType<NarrationEntry>().Select(e => e.Text);

    private static bool Logged(Flip7Room room, string category, string text) =>
        room.Feed.OfType<NarrationEntry>().Any(e => e.Category == category && e.Text.Contains(text));

    [Test]
    public async Task Draws_are_logged_as_they_are_dealt()
    {
        var room = Started3(Num(3), Num(4), Num(5));   // deal: Bob 3, Carol 4, Alice 5

        await Assert.That(Texts(room)).Contains("Bob drew 3.");
        await Assert.That(Texts(room)).Contains("Carol drew 4.");
        await Assert.That(Texts(room)).Contains("Alice drew 5.");
    }

    [Test]
    public async Task A_bust_is_logged()
    {
        var room = Started3(Num(5), Num(4), Num(3), Num(5));   // Bob holds 5, then hits a second 5
        room.Hit(Bob);

        await Assert.That(Logged(room, "bust", "Bob busts on a second 5.")).IsTrue();
    }

    [Test]
    public async Task Staying_is_logged()
    {
        var room = Started3(Num(5), Num(4), Num(3));
        room.Stay(Bob);

        await Assert.That(Logged(room, "stay", "Bob stays.")).IsTrue();
    }

    [Test]
    public async Task A_freeze_records_who_played_it_and_on_whom()
    {
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(Logged(room, "freeze", "Bob freezes Carol.")).IsTrue();
    }

    [Test]
    public async Task A_flip_three_records_who_drew_it_and_on_whom()
    {
        var room = Started3(Num(1), Num(2), Num(3), FlipThree);
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        await Assert.That(Logged(room, "flip-three", "Bob flips three on Carol.")).IsTrue();
    }

    [Test]
    public async Task Playing_an_action_on_yourself_reads_naturally()
    {
        // Alice is the last one in, so the Freeze auto-lands on her — no choice to make.
        var room = Started3(Num(1), Num(2), Num(3), Freeze);
        room.Stay(Bob);
        room.Stay(Carol);
        room.Hit(Alice);   // Alice draws the Freeze; she's the only active player

        await Assert.That(Logged(room, "freeze", "Alice freezes themselves.")).IsTrue();
    }

    [Test]
    public async Task A_second_chance_save_is_logged()
    {
        var room = Started3(Num(5), Num(1), Num(2), SecondChance, Num(5));
        room.Hit(Bob);      // Bob draws and holds a Second Chance
        room.Stay(Carol);
        room.Stay(Alice);
        room.Hit(Bob);      // duplicate 5 — the Second Chance cancels it

        await Assert.That(Logged(room, "second-chance", "Bob's Second Chance cancels the second 5.")).IsTrue();
    }

    [Test]
    public async Task Gifting_a_spare_Second_Chance_is_logged()
    {
        // Bob already holds one, draws another, and the only active player without one is Carol,
        // so it's handed to her.
        var room = Started3(Num(1), Num(2), Num(3), SecondChance, Num(5), SecondChance);
        room.Hit(Bob);     // Bob draws and holds a Second Chance
        room.Hit(Carol);   // Carol takes a card, stays active
        room.Stay(Alice);
        room.Hit(Bob);     // Bob's second Second Chance goes to Carol

        await Assert.That(Logged(room, "second-chance", "Bob gives Carol a Second Chance.")).IsTrue();
    }

    [Test]
    public async Task Flip_7_is_logged()
    {
        var room = Started3(
            Num(1), Num(2), Num(3),   // deal
            Num(4), Num(9),
            Num(5), Num(10),
            Num(6), Num(11),
            Num(7), Num(12),
            Num(8), Num(0),
            Num(9));                  // Carol's seventh distinct number
        room.Stay(Bob);
        for (var i = 0; i < 5; i++)
        {
            room.Hit(Carol);
            room.Hit(Alice);
        }

        room.Hit(Carol);

        await Assert.That(Logged(room, "flip7", "Carol hits Flip 7!")).IsTrue();
    }

    [Test]
    public async Task The_feed_keeps_earlier_rounds()
    {
        var room = Started3(Num(5), Num(4), Num(3));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);           // round 1 ends, nobody has won

        room.NextRound(Alice);      // deal round 2

        // The feed doesn't clear between rounds — the new round is marked, and round one's lines
        // are still there to scroll back to.
        await Assert.That(Texts(room).Any(t => t.Contains("Round 2"))).IsTrue();
        await Assert.That(Texts(room).Any(t => t == "Bob stays.")).IsTrue();
    }
}
