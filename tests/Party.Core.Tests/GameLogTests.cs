namespace Party.Core.Tests;

/// <summary>The log is a plain, ordered, categorised list of lines. The mechanism owns nothing else.</summary>
public class GameLogTests
{
    [Test]
    public async Task Entries_keep_their_order_and_a_rising_sequence()
    {
        var log = new GameLog();
        log.Add("first", "info");
        log.Add("second", "draw");

        // Assert positionally: IsEquivalentTo is order-insensitive and wouldn't catch a reversal.
        await Assert.That(log.Entries[0].Text).IsEqualTo("first");
        await Assert.That(log.Entries[0].Sequence).IsEqualTo(0);
        await Assert.That(log.Entries[1].Text).IsEqualTo("second");
        await Assert.That(log.Entries[1].Sequence).IsEqualTo(1);
        await Assert.That(log.Entries[1].Category).IsEqualTo("draw");
    }

    [Test]
    public async Task Add_defaults_to_the_info_category()
    {
        var log = new GameLog();
        log.Add("plain");

        await Assert.That(log.Entries[0].Category).IsEqualTo("info");
    }

    [Test]
    public async Task Clearing_empties_it_and_resets_the_sequence()
    {
        var log = new GameLog();
        log.Add("a");
        log.Add("b");

        log.Clear();
        log.Add("fresh");

        await Assert.That(log.Entries).Count().IsEqualTo(1);
        await Assert.That(log.Entries[0].Sequence).IsEqualTo(0);
    }
}
