using Bunit;
using Party.Core;
using Party.Web.Components.Game;

namespace Party.Web.Tests.Components;

/// <summary>The one feed panel: chat and narration in a single stream, newest at the bottom.</summary>
public class RoomFeedTests
{
    private static readonly Guid Me = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static IRenderedComponent<RoomFeed> Render(BunitContext ctx, IReadOnlyList<FeedEntry> feed, Action<string>? onSend = null) =>
        ctx.Render<RoomFeed>(p => p
            .Add(x => x.Feed, feed)
            .Add(x => x.MyId, Me)
            .Add(x => x.OnSend, s => onSend?.Invoke(s)));

    [Test]
    public async Task It_shows_chat_and_narration_and_flags_your_own_chat()
    {
        using var ctx = new BunitContext();
        var feed = new List<FeedEntry>
        {
            new ChatEntry(0, Other, "Bob", "hi all"),
            new NarrationEntry(1, "Bob drew 7", "draw"),
            new ChatEntry(2, Me, "Me", "hello"),
        };
        var panel = Render(ctx, feed);

        await Assert.That(panel.FindAll(".feed-chat")).Count().IsEqualTo(2);
        await Assert.That(panel.FindAll(".feed-note")).Count().IsEqualTo(1);
        await Assert.That(panel.FindAll(".feed-chat.mine")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Narration_carries_its_category_for_styling()
    {
        using var ctx = new BunitContext();
        var panel = Render(ctx, [new NarrationEntry(0, "Bob freezes Carol.", "freeze")]);

        await Assert.That(panel.Find(".feed-note").ClassList).Contains("freeze");
    }

    [Test]
    public async Task It_lists_newest_at_the_bottom()
    {
        using var ctx = new BunitContext();
        var panel = Render(ctx,
        [
            new ChatEntry(0, Other, "Bob", "first"),
            new ChatEntry(1, Other, "Bob", "second"),
        ]);

        // column-reverse fed newest-first: the newest is the first DOM child (visually bottom).
        var lines = panel.FindAll(".feed-chat");
        await Assert.That(lines[0].TextContent).Contains("second");
    }

    [Test]
    public async Task An_empty_feed_prompts_you_to_say_hi()
    {
        using var ctx = new BunitContext();
        var panel = Render(ctx, []);

        await Assert.That(panel.FindAll(".feed-chat")).IsEmpty();
        await Assert.That(panel.Find(".feed-empty").TextContent).Contains("say hi");
    }

    [Test]
    public async Task Sending_emits_the_trimmed_text_and_clears_the_box()
    {
        using var ctx = new BunitContext();
        string? sent = null;
        var panel = Render(ctx, [], s => sent = s);

        panel.Find(".feed-input").Input("  well played  ");
        await panel.Find(".feed-form").SubmitAsync();

        await Assert.That(sent).IsEqualTo("well played");
        await Assert.That(panel.Find(".feed-input").GetAttribute("value")).IsNullOrEmpty();
    }

    [Test]
    public async Task A_blank_draft_sends_nothing()
    {
        using var ctx = new BunitContext();
        var sends = 0;
        var panel = Render(ctx, [], _ => sends++);

        panel.Find(".feed-input").Input("   ");
        await panel.Find(".feed-form").SubmitAsync();

        await Assert.That(sends).IsEqualTo(0);
    }
}
