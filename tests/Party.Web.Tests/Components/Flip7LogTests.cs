using Bunit;
using Party.Core;
using Party.Web.Components.Game;

namespace Party.Web.Tests.Components;

/// <summary>The round log panel: newest first, styled by category, and honest when empty.</summary>
public class Flip7LogTests
{
    private static IRenderedComponent<Flip7Log> Render(BunitContext ctx, params GameLogEntry[] entries) =>
        ctx.Render<Flip7Log>(p => p.Add(x => x.Entries, entries));

    [Test]
    public async Task It_lists_entries_newest_first_and_tags_the_category()
    {
        using var ctx = new BunitContext();
        var log = Render(ctx,
            new GameLogEntry(0, "Bob drew 3.", "draw"),
            new GameLogEntry(1, "Bob busts on a second 3.", "bust"));

        var lines = log.FindAll(".f7log-line");
        await Assert.That(lines).Count().IsEqualTo(2);
        await Assert.That(lines[0].TextContent).Contains("busts");        // newest first
        await Assert.That(lines[0].ClassList).Contains("bust");
        await Assert.That(lines[1].TextContent).Contains("drew");
    }

    [Test]
    public async Task An_empty_log_says_nothing_yet()
    {
        using var ctx = new BunitContext();
        var log = Render(ctx);

        await Assert.That(log.FindAll(".f7log-line")).IsEmpty();
        await Assert.That(log.Find(".f7empty").TextContent).Contains("Nothing yet");
    }
}
